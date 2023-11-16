using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Serialization.Response;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Serialization;
using SIL.Transcriber.Utility.Extensions;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using static SIL.Transcriber.Utility.ResourceHelpers;
using SIL.Transcriber.Utility;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Amazon.Lambda.Core;
using System.Security.Cryptography;
using Amazon.S3;
using System.Net.Mime;
using System.Collections.Generic;


namespace SIL.Transcriber.Services
{
    public class OfflineDataService : IOfflineDataService
    {
        protected readonly AppDbContext dbContext;
        protected readonly MediafileService mediaService;
        protected CurrentUserRepository CurrentUserRepository { get; }

        readonly private IS3Service _S3Service;
        readonly private ISQSService _SQSService;
        private const string ImportFolder = "imports";
        private const string ExportFolder = "exports";

        protected ILogger<OfflineDataService> Logger { get; set; }

        private readonly IResourceGraph _resourceGraph;
        private readonly IResourceDefinitionAccessor _resourceDefinitionAccessor;
        private readonly IMetaBuilder _metaBuilder;
        private readonly IJsonApiOptions _options;
        readonly private HttpContext? HttpContext;

        private readonly Dictionary<string,char> TableOrder = new()
        {
            {Tables.Users,'A'},
            {Tables.ActivityStates,'B'},
            {Tables.Integrations,'B'},
            {Tables.Organizations,'B'},
            {Tables.PassageTypes,'B'},
            {Tables.PlanTypes,'B'},
            {Tables.ProjectTypes,'B'},
            {Tables.Roles,'B'},
            {Tables.WorkflowSteps,'B'},
            {Tables.ArtifactCategorys,'C'},
            {Tables.ArtifactTypes,'C'},
            {Tables.Groups,'C'},
            {Tables.OrganizationMemberships,'C'},
            {Tables.OrgKeyTerms,'C'},
            {Tables.OrgWorkflowSteps,'C'},
            {Tables.GroupMemberships,'D'},
            {Tables.Projects,'D'},
            {Tables.Invitations,'D'},
            {Tables.Plans, 'E'},
            {Tables.ProjectIntegrations,'E'},
            {Tables.Sections,'F'},
            {Tables.Passages,'G'},
            {Tables.Mediafiles,'H'},
            {Tables.OrgKeyTermReferences,'H'},
            {Tables.PassageStateChanges,'H'},
            {Tables.OrgKeyTermTargets,'I'},
            {Tables.SectionResources,'I'},
            {Tables.Discussions,'I'},
            {Tables.IntellectualPropertys,'I'},
            {Tables.SharedResources, 'I' },
            {Tables.Comments,'J'},
            {Tables.SectionResourceUsers,'J'},
            {Tables.SharedResourceReferences, 'J' }
        };
        Dictionary<int, int>? ArtifactCategoryMap = null;
        Dictionary<int, int>? ArtifactTypesMap = null;
        Dictionary<int, int>? OrgworkflowstepMap = null;
        Dictionary<int, int>? SectionMap = null;
        Dictionary<int, int>? PassageMap  = null;
        Dictionary<int, int>? MediafileMap = null;
        Dictionary<int, int>? DiscussionMap = null;
        Dictionary<int, int>? SectionResourceMap = null;
        public OfflineDataService(
            AppDbContextResolver contextResolver,
            MediafileService MediaService,
            CurrentUserRepository currentUserRepository,
            IS3Service service,
            ISQSService sqsService,
            ILoggerFactory loggerFactory,
            IResourceGraph resourceGraph,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            IMetaBuilder metaBuilder,
            IJsonApiOptions options,
            IHttpContextAccessor httpContextAccessor

        )
        {
            HttpContext = httpContextAccessor.HttpContext;
            dbContext = (AppDbContext)contextResolver.GetContext();
            mediaService = MediaService;
            CurrentUserRepository = currentUserRepository;
            _S3Service = service;
            _SQSService = sqsService;
            Logger = loggerFactory.CreateLogger<OfflineDataService>();
            _resourceGraph = resourceGraph;
            _resourceDefinitionAccessor = resourceDefinitionAccessor;
            _metaBuilder = metaBuilder;
            _options = options;
        }

        private User? CurrentUser()
        {
            return CurrentUserRepository.GetCurrentUser();
        }

        private static void WriteEntry(ZipArchiveEntry entry, string contents)
        {
            using StreamWriter sw = new (entry.Open());
            sw.WriteLine(contents);
        }

        private static DateTime AddCheckEntry(ZipArchive zipArchive, int version)
        {
            ZipArchiveEntry entry = zipArchive.CreateEntry(
                "SILTranscriber",
                CompressionLevel.Fastest
            );
            DateTime dt = DateTime.UtcNow;
            WriteEntry(entry, dt.ToString("o"));
            entry = zipArchive.CreateEntry("Version", CompressionLevel.Fastest);
            WriteEntry(entry, version.ToString());
            return dt;
        }

        private string ToJson<TResource>(IEnumerable<TResource> resources)
            where TResource : class, IIdentifiable
        {
            string? withIncludes =
            SerializerHelpers.ResourceListToJson<TResource>(
                resources,
                _resourceGraph,
                _options,
                _resourceDefinitionAccessor,
                _metaBuilder
            );
            if (withIncludes.Contains("included"))
            {
                dynamic tmp = JObject.Parse(withIncludes);
                tmp.Remove("included");
                return tmp.ToString();
            }
            return withIncludes;
        }

        private void AddJsonEntry<TResource>(
            ZipArchive zipArchive,
            string table,
            IList<TResource> list
        ) where TResource : class, IIdentifiable
        {
            ZipArchiveEntry entry = zipArchive.CreateEntry(
                "data/" + TableOrder.GetValueOrDefault(table, 'Z') + "_" + table + ".json",
                CompressionLevel.SmallestSize
            );
            WriteEntry(entry, ToJson(list));
        }

        private static void AddEafEntry(ZipArchive zipArchive, string name, string eafXML)
        {
            if (!string.IsNullOrEmpty(eafXML))
            {
                ZipArchiveEntry entry = zipArchive.CreateEntry(
                    "media/" + Path.ChangeExtension(name, ".eaf"),
                    CompressionLevel.Optimal
                );
                WriteEntry(entry, eafXML);
            }
        }

        private static bool AddStreamEntry(
            ZipArchive zipArchive,
            Stream fileStream,
            string dir,
            string newName
        )
        {
            if (fileStream != null)
            {
                ZipArchiveEntry entry = zipArchive.CreateEntry(
                    dir + newName,
                    CompressionLevel.Optimal
                );
                using Stream zipEntryStream = entry.Open();
                //Copy the attachment stream to the zip entry stream
                fileStream.CopyTo(zipEntryStream);
                return true;
            }
            return false;
        }

        private static bool AddStreamEntry(
            ZipArchive zipArchive,
            string url,
            string dir,
            string newName
        )
        {
            Stream? s = GetStreamFromUrlAsync(url).Result;
            return s != null && AddStreamEntry(zipArchive, s, dir, newName);
        }

        private static async Task<Stream?> GetStreamFromUrlAsync(string url)
        {
            try
            {
                using HttpClient client = new ();
                using HttpResponseMessage response = await client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead
                );
                using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();
                MemoryStream mem = new ();
                streamToReadFrom.CopyTo(mem);
                mem.Position = 0;
                return mem;
            }
            catch
            {
                return null;
            }
        }

        private static void AddOrgLogos(ZipArchive zipArchive, List<Organization> orgs)
        {
            orgs.ForEach(o => {
                if (!string.IsNullOrEmpty(o.LogoUrl))
                {
                    _ = AddStreamEntry(zipArchive, o.LogoUrl, "logos/", o.Slug + ".png");
                    //    o.LogoUrl = "logos/" + o.Slug + ".png";
                    //else
                    //    o.LogoUrl = null;
                }
            });
        }

        private static void AddUserAvatars(ZipArchive zipArchive, List<User> users)
        {
            users.ForEach(u => {
                if (!string.IsNullOrEmpty(u.AvatarUrl))
                {
                    _ = AddStreamEntry(
                        zipArchive,
                        u.AvatarUrl,
                        "avatars/",
                        u.Id.ToString() + u.FamilyName + ".png"
                    );
                    //u.AvatarUrl = "avatars/" + u.Id.ToString() + u.FamilyName + ".png";
                }
            });
        }

        private bool AddMediaEaf(
            int check,
            DateTime dtBail,
            ref int completed,
            ZipArchive zipArchive,
            List<Mediafile> media,
            string? nameTemplate
        )
        {
            if (DateTime.Now > dtBail)
                return false;
            if (completed <= check)
            {
                foreach (Mediafile m in media)
                {
                    AddEafEntry(zipArchive, NameFromTemplate(m, nameTemplate), mediaService.EAF(m));
                }
                completed++;
            }
            return true;
        }

        /*
        private bool AddMedia(int check, DateTime dtBail, ref int completed, ZipArchive zipArchive, List<Mediafile> media)
        {
            foreach (Mediafile m in media)
            {
                Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
                if (DateTime.Now > dtBail) return false;
                if (completed <= check)
                {
                    if (!string.IsNullOrEmpty(m.S3File)) {
                        S3Response response = mediaService.GetFile(m.Id).Result;
                        AddStreamEntry(zipArchive, response.FileStream, "media/", m.S3File);
                        m.AudioUrl = "media/" + m.S3File;
                        AddEafEntry(zipArchive, m.S3File, mediaService.EAF(m));
                    }
                    completed++;
                }
                check++;
            };
            return true;
        }
        */
        private static void AddFont(ZipArchive zipArchive, HttpClient client, string cssFile)
        {
            string bucket = "https://s3.amazonaws.com/fonts.siltranscriber.org/";
            try
            {
                /* read the css file */
                string url = bucket + cssFile;
                HttpResponseMessage? response = client.GetAsync(url).Result;
                string css = response.Content.ReadAsStringAsync().Result;

                // example: /* CharisSIL.css */
                /*
                @font - face {
                    font - family: CharisSIL;
                    src: url('https://s3.amazonaws.com/fonts.siltranscriber.org/CharisSIL-R.ttf')
                  }
                */
                //extract the url
                int start = css.IndexOf("url('") + 4;
                if (start != 3)
                {
                    int end = css.IndexOf("')", start);
                    url = css [start..end];
                    string fontFile = url[(url.LastIndexOf("/") + 1)..];
                    url = bucket + fontFile;
                    _ = AddStreamEntry(zipArchive, url, "fonts/", fontFile);
                    css = css [..(start + 1)] + fontFile + css [end..];
                }
                ZipArchiveEntry entry = zipArchive.CreateEntry(
                    "fonts/" + cssFile,
                    CompressionLevel.Fastest
                );
                WriteEntry(entry, css);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Font file not found {0}", cssFile);
                Console.WriteLine(ex);
            }
        }

        private static void AddFonts(ZipArchive zipArchive, IEnumerable<string> fonts)
        {
            using HttpClient client = new ();
            foreach (string f in fonts)
            {
                string cssFile = f.Split(',')[0].Replace(" ", "") + ".css";
                AddFont(zipArchive, client, cssFile);
            }
        }

        /// <summary>
        /// Strip illegal chars and reserved words from a candidate filename (should not include the directory path)
        /// </summary>
        /// <remarks>
        /// http://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
        /// </remarks>
        public static string CleanFileName(string filename)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(
                new string(Path.GetInvalidFileNameChars()) + "'()"
            );
            string invalidReStr = string.Format(@"[{0}, ]+", invalidChars);

            string[] reservedWords = new[]
            {
                "CON",
                "PRN",
                "AUX",
                "CLOCK$",
                "NUL",
                "COM0",
                "COM1",
                "COM2",
                "COM3",
                "COM4",
                "COM5",
                "COM6",
                "COM7",
                "COM8",
                "COM9",
                "LPT0",
                "LPT1",
                "LPT2",
                "LPT3",
                "LPT4",
                "LPT5",
                "LPT6",
                "LPT7",
                "LPT8",
                "LPT9"
            };

            string sanitizedName = System.Text.RegularExpressions.Regex.Replace(
                filename,
                invalidReStr,
                "_"
            );
            while (sanitizedName.IndexOf("__") > -1)
                sanitizedName = sanitizedName.Replace("__", "_");

            foreach (string reservedWord in reservedWords)
            {
                string reservedWordPattern = string.Format("^{0}(\\.|$)", reservedWord);
                sanitizedName = System.Text.RegularExpressions.Regex.Replace(
                    sanitizedName,
                    reservedWordPattern,
                    "_reservedWord_$1",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }

            return sanitizedName;
        }

        private bool CheckAdd<TResource>(
            int check,
            DateTime dtBail,
            ref int completed,
            ZipArchive zipArchive,
            string table,
            IList<TResource> list
        ) where TResource : class, IIdentifiable
        {
            Logger.LogInformation("{check} : {dt} {dtBail}", check, DateTime.Now, dtBail);
            if (DateTime.Now > dtBail)
                return false;
            if (completed <= check)
            {
                AddJsonEntry(zipArchive, table, list);
                completed++;
            }
            return true;
        }

        private Fileresponse CheckProgress(string fileName, int lastAdd)
        {
            int startNext;
            string err = "";
            try
            {
                Stream ms = OpenFile(fileName + ".sss", out DateTime writeTime);
                StreamReader reader = new(ms);
                string data = reader.ReadToEnd();
                bool recent = writeTime > DateTime.Now.AddMinutes(data.Contains("writing") ? -8 : -2);
                if (recent)
                {
                    if (data.IndexOf("|") > 0)
                    {
                        err = data [(data.IndexOf("|") + 1)..];
                        data = data [..data.IndexOf("|")];
                    }
                    bool media = data.Contains(" media");
                    if (media)
                        data = data [..data.IndexOf(" media")];
                    if (!int.TryParse(data, out startNext))
                        startNext = 0;
                    if (media)
                        startNext += lastAdd;
                }
                else
                    startNext = 0;
            }
            catch
            {
                //it's not there yet...
                Logger.LogWarning("{sf} status file not available", fileName + ".sss");
                startNext = lastAdd + 1;
            }
            if (startNext < 0)
            {
                try
                {
                    S3Response resp = _S3Service.RemoveFile(fileName + ".sss", ExportFolder).Result;
                    resp = _S3Service.RemoveFile(fileName + ".tmp", ExportFolder).Result;
                }
                catch { }
            }
            else
                startNext = Math.Max(startNext, lastAdd + 1);

            string contentType = string.Concat("application/", (Path.GetExtension(fileName))[1..]);
            return new Fileresponse()
            {
                Message = startNext == -2 ? err : fileName,
                //get a signedurl for it if we're done
                FileURL =
                    startNext == -1
                        ? _S3Service.SignedUrlForGet(fileName, ExportFolder, contentType).Message
                        : "",
                Status =
                    startNext == -1
                        ? System.Net.HttpStatusCode.OK
                        : startNext == -2
                            ? System.Net.HttpStatusCode.RequestEntityTooLarge
                            : System.Net.HttpStatusCode.PartialContent,
                ContentType = contentType,
                Id = startNext,
            };
        }

        private Stream OpenFile(string fileName, out DateTime writeTime)
        {
            S3Response s3response = _S3Service.ReadObjectDataAsync(fileName, ExportFolder, true).Result;

            _ = DateTime.TryParse(s3response.Message, out writeTime);
            return s3response.FileStream ?? throw (new Exception("Export in progress " + fileName + "not found."));
        }

        private Stream GetMemoryStream(int start, string fileName, string ext)
        {
            Stream ms;
            if (start == 0)
            {
                ms = new MemoryStream();
                try
                {
                    S3Response resp = _S3Service.RemoveFile(fileName + ".sss", ExportFolder).Result;
                }
                catch { }
                ;
            }
            else
            {
                ms = OpenFile(fileName + ext, out _);
            }
            return ms;
        }

        private Fileresponse WriteMemoryStream(
            Stream ms,
            string fileName,
            int startNext,
            string ext
        )
        {
            S3Response s3response;
            string contentType = string.Concat("application/", ext[1..]);
            ms.Position = 0;
            fileName += ext;
            s3response = _S3Service
                .UploadFileAsync(ms, true, contentType, fileName, ExportFolder)
                .Result;
            return s3response.Status == HttpStatusCode.OK
                ? new Fileresponse()
                {
                    Message = fileName,
                    FileURL = "",
                    Status = HttpStatusCode.PartialContent,
                    ContentType = contentType,
                    Id = Math.Abs(startNext),
                }
                : throw new Exception(s3response.Message);
        }

        private void AddBurritoMeta(
            ZipArchive zipArchive,
            Project project,
            List<Mediafile> mediafiles
        )
        {
            Dictionary<string, string> mimeMap = new()
            {
                { "mp3", "audio/mpeg" },
                { "webm", "audio/webm;codecs=opus" },
                { "mka", "audio/webm;codecs=pcm" },
                { "wav", "audio/wav" },
                { "m4a", "audio/x-m4a" },
                { "ogg", "audio/ogg;codecs=opus" },
                { "itf", "application/itf" },
                { "ptf", "application/ptf" },
                { "jpg", "image/jpeg" },
                { "svg", "image/svg+xml" },
                { "png", "image/png" },
            };

            ZipArchiveEntry entry = zipArchive.CreateEntry(
                "metadata.json",
                CompressionLevel.Fastest
            );
            string metastr = LoadResource("burritometa.json");
            Dictionary<string, List<string>> scopes = new();
            List<string> formats = new();

            dynamic? root = Newtonsoft.Json.JsonConvert.DeserializeObject(metastr) ?? throw new Exception("Bad Meta" + metastr);
            root.meta.version = "0.3.1";
            root.meta.category = "source";
            root.meta.generator.softwareName = "SIL Audio Project Manager";
            root.meta.generator.softwareVersion =
                dbContext.Currentversions.FirstOrDefault()?.DesktopVersion ?? "unknown";
            root.meta.generator.userName = CurrentUser()?.Name ?? "unknown";
            root.meta.defaultLanguage = project.Language;
            root.meta.dateCreated = DateTime.Now.ToString("o");
            root.identification.name.en = project.Name;
            root.identification.description.en = project.Description;
            root.languages [0].tag = project.Language;
            root.languages [0].name.en = project.LanguageName;

            mediafiles.ForEach(m => {
                //get stored book and ref out of audioquality
                string[] split = (m.AudioQuality ?? "|").Split("|");
                string book = split[0];
                string reference = split[1];
                if (!scopes.ContainsKey(book))
                    scopes.Add(book, new List<string>());
                scopes [book].Add(reference);
                string ext = Path.GetExtension(m.AudioUrl ?? "").TrimStart('.');
                if (!formats.Contains(ext))
                    formats.Add(ext);
                root.ingredients [m.AudioUrl] = new JObject();
                if (mimeMap.ContainsKey(ext))
                    root.ingredients [m.AudioUrl].mimeType = mimeMap [ext];
                root.ingredients [m.AudioUrl].size = m.Filesize;
                string scopestr = string.Format("{{[{0}]:[{1}]}}", book, reference);
                root.ingredients [m.AudioUrl].scope = new JObject();
                root.ingredients [m.AudioUrl].scope [book] = JToken.FromObject(
                    new string [] { reference }
                );
            });
            for (int n = 0; n < formats.Count; n++)
            {
                string name = "format" + (n + 1).ToString();
                root.type.flavorType.flavor.formats [name] = new JObject();
                root.type.flavorType.flavor.formats [name].compression = formats [n];
            }
            foreach (KeyValuePair<string, List<string>> item in scopes)
            {
                root.type.flavorType.currentScope [item.Key] = JToken.FromObject(
                    item.Value.ToArray()
                );
            }
            WriteEntry(
                entry,
                Newtonsoft.Json.JsonConvert.SerializeObject(
                    root,
                    Newtonsoft.Json.Formatting.Indented
                )
            );
        }

        private static string ScriptureFullPath(string? language, Passage? passage, Mediafile m)
        {
            return passage == null || language == null
                ? ""
                : "release/audio/"
                + passage.Book
                + "/"
                + string.Format(
                    "{0}-{1}-{2}-{3}-{4}v{5}{6}",
                    language,
                    passage.Book,
                    ToStr(passage.StartChapter).PadLeft(3, '0'),
                    ToStr(passage.StartVerse).PadLeft(3, '0'),
                    ToStr(passage.EndVerse).PadLeft(3, '0'),
                    m.VersionNumber,
                    Path.GetExtension(m.S3File)
                );
        }
        private static string IPFullPath(Mediafile m)
        {
            //todo...what should go here?
            return string.Format("ip/{0}-{1}", m.PerformedBy, Path.GetExtension(m.S3File));
        }
        private List<Mediafile> AddBurritoMedia(
            ZipArchive zipArchive,
            Project project,
            List<Mediafile> mediafiles
        )
        {
            IEnumerable<Intellectualproperty>? ip = mediafiles.Join(dbContext.IntellectualPropertys.Where(ip => ip.OrganizationId == project.OrganizationId), m=> m.PerformedBy, i => i.RightsHolder, (m, i) => i);
            List<Mediafile>? ipMedia = ip.Join(dbContext.Mediafiles, ip => ip.ReleaseMediafileId, m => m.Id, (ip, m) => m).ToList();

            mediafiles.ForEach(m => {
                Passage? passage = dbContext.Passages
                    .Where(p => p.Id == m.PassageId)
                    .FirstOrDefault();
                //S3File has just the filename
                //AudioUrl has the signed GetUrl which has the path + filename as url (so spaces changed etc) + signed stuff
                //change the audioUrl to have the offline path + filename
                //change the s3File to have the onlinepath + filename
                m.AudioQuality = passage?.Book + "|" + passage?.Reference; //store these here temporarily
                m.AudioUrl = ScriptureFullPath(project?.Language, passage, m);
                m.S3File = mediaService.DirectoryName(m) + "/" + m.S3File;
            });
            ipMedia.ForEach(m => {
                m.AudioUrl = IPFullPath(m);
                m.S3File = mediaService.DirectoryName(m) + "/" + m.S3File;
            });
            AddJsonEntry(zipArchive, "attachedmediafiles", mediafiles.Concat(ipMedia).ToList<Mediafile>());
            return mediafiles;
        }
        private static string ToStr(int? value)
        {
            return value?.ToString()??"";
        }   
        private string NameFromTemplate(Mediafile m, string? nameTemplate)
        {
            //expecting template to have
            //{BOOK}{SECT}{TITLE}{PASS}{REF}{VERS}
            Passage? passage = dbContext.Passages.Find(m.PassageId??0);
            Section? section = dbContext.Sections.Find(passage?.SectionId??0);

            if (nameTemplate == null || passage == null || section == null)
                return m.S3File ?? "";

            bool flat = passage.Sequencenum == 1 && (dbContext.Plans.Find(section.PlanId)?.Flat ?? false);
            string name = nameTemplate.Replace("{BOOK}", passage.Book)
                                .Replace("{SECT}", section.Sequencenum.ToString().PadLeft(3, '0'))
                                .Replace("{TITLE}", section.Name)
                                .Replace("{VERS}", "v" + (m.VersionNumber??1).ToString());
            if (!flat)
                name = name.Replace("{PASS}", passage.Sequencenum.ToString().PadLeft(3, '0'));
            if (name.Contains("{REF}"))
            {
                string pref = passage.StartChapter > 0
                        ? passage.StartChapter == passage.EndChapter ? string.Format(
                                "{0}_{1}{2}",
                                ToStr(passage.StartChapter).PadLeft(3, '0'),
                                ToStr(passage.StartVerse).PadLeft(3, '0'),
                                passage.StartVerse == passage.EndVerse ? "" :
                                '-' + ToStr(passage.EndVerse).PadLeft(3, '0')
                            ) : string.Format(
                                "{0}_{1}-{2}_{3}",
                                ToStr(passage.StartChapter).PadLeft(3, '0'),
                                ToStr(passage.StartVerse).PadLeft(3, '0'),
                                ToStr(passage.EndChapter).PadLeft(3, '0'),
                                ToStr(passage.EndVerse).PadLeft(3, '0'))
                        : passage.Reference ?? "";
                if (pref.Length == 0 && !nameTemplate.Contains("{PASS}"))
                {   //do I have enough info without ref?
                    if (flat && !nameTemplate.Contains("{SECT}") && !nameTemplate.Contains("{TITLE}"))
                        pref = section.Name ?? "S"+section.Sequencenum.ToString().PadLeft(3, '0');
                    else if (!flat)
                        pref = (nameTemplate.Contains("{SECT}") ? "" : "S"+section.Sequencenum.ToString().PadLeft(3, '0')) + "_P" + passage.Sequencenum.ToString().PadLeft(3, '0');
                }
                name = name.Replace("{REF}", pref);
            }
            if (name.Replace("_", "") == "" || name.StartsWith("_v"))
                name = "S" + section.Sequencenum.ToString().PadLeft(3, '0') + (flat ? "" : "_P" + passage.Sequencenum.ToString().PadLeft(3, '0'));
            return CleanFileName(name) + Path.GetExtension(m.S3File);
        }
        private void AddAttachedMedia(ZipArchive zipArchive, List<Mediafile> mediafiles, string? nameTemplate)
        {
            mediafiles.ForEach(m => {
                //S3File has just the filename
                //AudioUrl has the signed GetUrl which has the path + filename as url (so spaces changed etc) + signed stuff
                //change the audioUrl to have the offline path + filename
                //change the s3File to have the onlinepath + filename 
                m.AudioUrl = "media/" + NameFromTemplate(m, nameTemplate);
                m.S3File = mediaService.DirectoryName(m) + "/" + m.S3File;
            });
            AddJsonEntry(zipArchive, "attachedmediafiles", mediafiles);
        }

        public Fileresponse ExportProjectAudio(
            int projectid,
            string artifactType,
            string? idList,
            int start,
            bool addElan,
            string? nameTemplate
        )
        {
            int LAST_ADD = 0;
            const string ext = ".zip";
            int startNext = start;

            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == projectid);
            Project project = projects.First();
            string fileName = string.Format(
                "{0}{1}_{2}_{3}",
                addElan ? "Elan" : "Audio",
                CleanFileName(project.Name + artifactType),
                project.Id.ToString(),
                CurrentUser()?.Id
            );
            if (start > LAST_ADD)
                return CheckProgress(fileName + ext, LAST_ADD);
            if (start == 0)
            {
                Stream ms = GetMemoryStream(start, fileName, ext);
                using (ZipArchive zipArchive = new(ms, ZipArchiveMode.Update, true))
                {
                    DateTime exported = AddCheckEntry(
                        zipArchive,
                        dbContext.Currentversions.FirstOrDefault()?.SchemaVersion ?? 5
                    );
                    List<Mediafile> mediafiles = dbContext.Mediafiles
                        .Where(x => (idList ?? "").Contains("," + x.Id.ToString() + ","))
                        .ToList();
                    AddJsonEntry(zipArchive, Tables.Mediafiles, mediafiles);
                    if (addElan)
                        _ = AddMediaEaf(
                            0,
                            DateTime.Now.AddSeconds(15),
                            ref startNext,
                            zipArchive,
                            mediafiles, nameTemplate
                        );
                    AddAttachedMedia(zipArchive, mediafiles, nameTemplate);
                    startNext = 1;
                }
                WriteMemoryStream(ms, fileName, startNext, ext);
            }
            string id= _SQSService.SendExportMessage(project.Id, ExportFolder, fileName + ext, 0);
            return new()
            {
                Message = fileName + ext,
                FileURL = "",
                Status = HttpStatusCode.PartialContent,
                ContentType = "application/zip",
                Id = Math.Abs(startNext),
            };
        }

        public Fileresponse ExportBurrito(int projectid, string? idList, int start)
        {
            int LAST_ADD = 0;
            const string ext = ".zip";
            int startNext = start;

            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == projectid);
            Project project = projects.First();
            string fileName = string.Format(
                "Burrito{0}_{1}_{2}",
                CleanFileName(project.Name),
                project.Id.ToString(),
                CurrentUser()?.Id
            );

            if (start > LAST_ADD)
                return CheckProgress(fileName + ext, LAST_ADD);

            Stream ms = GetMemoryStream(start, fileName, ext);
            using (ZipArchive zipArchive = new(ms, ZipArchiveMode.Update, true))
            {
                if (start == 0)
                {
                    List<Mediafile> mediaList = dbContext.Mediafiles
                        .Where(x => (idList ?? "").Contains("," + x.Id.ToString() + ","))
                        .ToList();
                    mediaList = AddBurritoMedia(zipArchive, project, mediaList);
                    AddBurritoMeta(zipArchive, project, mediaList);
                    startNext = 1;
                }
            }
            WriteMemoryStream(ms, fileName, startNext, ext);
            //add the mediafiles
            string id= _SQSService.SendExportMessage(project.Id, ExportFolder, fileName + ext, 0);
            return new()
            {
                Message = fileName + ext,
                FileURL = "",
                Status = HttpStatusCode.PartialContent,
                ContentType = "application/zip",
                Id = Math.Abs(startNext),
            };
        }
        private IQueryable<Intellectualproperty> OrgIPs(IQueryable<Organization> orgs)
        {
            return dbContext.IntellectualPropertyData.Where(x => !x.Archived).Join(
                        orgs,
                        i => i.OrganizationId,
                        o => o.Id,
                        (i, o) => i);
        }
        private IQueryable<Mediafile> PlanMedia(IQueryable<Plan> plans, IQueryable<Intellectualproperty> ip, IQueryable<Passage>? supportingPassages = null)
        {
            IQueryable<Mediafile> xmediafiles = plans
                        .Join(dbContext.MediafilesData, p => p.Id, m => m.PlanId, (p, m) => m)
                        .Where(x => !x.Archived);
            IQueryable<Mediafile> ipmedia = ip.Join(dbContext.MediafilesData, ip => ip.ReleaseMediafileId, m=> m.Id, (ip, m) => m).Where(x => !x.Archived);
            IQueryable<Mediafile> supportingmedia = (supportingPassages ?? dbContext.Passages.Where(p => p.Id == -1)).Join(dbContext.MediafilesData, p => p.Id, m=> m.PassageId, (p, m) => m).Where(x => !x.Archived);
            IQueryable<Mediafile> myMedia = xmediafiles.Concat(ipmedia).Concat(supportingmedia).Distinct();
            return myMedia;
        }
        private IQueryable<Sectionresource> SectionResources(IQueryable<Section> sections)
        {
            return dbContext.Sectionresources
                        .Join(sections, r => r.SectionId, s => s.Id, (r, s) => r)
                        .Where(x => !x.Archived);
        }
        private IEnumerable<Mediafile> PlanSourceMedia(IQueryable<Sectionresource> sectionresources)
        {
            //get the mediafiles associated with section resources
            IQueryable<Mediafile> resourcemediafiles = dbContext.Mediafiles
                        .Join(sectionresources, m => m.Id, r => r.MediafileId, (m, r) => m)
                        .Where(x => !x.Archived);

            //now get any shared resource mediafiles associated with those mediafiles
            IEnumerable<Mediafile> sourcemediafiles = dbContext.Mediafiles
                        .Join(
                            resourcemediafiles,
                            m => m.PassageId,
                            r => r.ResourcePassageId,
                            (m, r) => m
                        )
                        .Where(x => x.ReadyToShare && !x.Archived).ToList();
            //pick just the highest version media per passage
            sourcemediafiles =
                from m in sourcemediafiles
                group m by m.PassageId into grp
                select grp.OrderByDescending(m => m.VersionNumber).FirstOrDefault();

            foreach (
                Mediafile mf in resourcemediafiles.ToList().Where(m => m.ResourcePassageId != null)
            )
            { //make sure we have the latest
                Mediafile? res = sourcemediafiles
                            .Where(s => s.PassageId == mf.ResourcePassageId)
                            .FirstOrDefault();
                if (res?.S3File != null)
                    mf.AudioUrl = _S3Service
                        .SignedUrlForGet(
                            res.S3File,
                            mediaService.DirectoryName(res),
                            res.ContentType ?? ""
                        )
                        .Message;
                _ = dbContext.Mediafiles.Update(mf);  //do I need to do a saveChanges?
            }
            dbContext.SaveChanges();
            return sourcemediafiles;
        }
        private static IQueryable<Mediafile> AttachedMedia(IQueryable<Mediafile> myMedia) {
            return myMedia
                        .Where(x => (x.PassageId != null || x.ArtifactTypeId != null) &&
                            x.ResourcePassageId == null && !x.Archived);

        }
        private IQueryable<Discussion> PlanDiscussions(IQueryable<Mediafile> myMedia)
        {
            return dbContext.Discussions
                        .Join(myMedia, d => d.MediafileId, m => m.Id, (d, m) => d)
                        .Where(x => !x.Archived);
        }
        public Fileresponse ExportProjectPTF(int projectId, int start)
        {
            const int LAST_ADD = 20;
            const string ext = ".ptf";
            int startNext = start;
            //give myself 15 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(15);

            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == projectId);
            Project project = projects.First();
            string fileName = string.Format(
                "APM{0}_{1}_{2}",
                CleanFileName(project.Name),
                project.Id.ToString(),
                CurrentUser()?.Id
            );
            if (start == 0)
            {
                Fileresponse? going = CheckProgress(fileName + ext, -1);
                if (going.Id != 0)
                    return going;
            }
            if (start > LAST_ADD)
            {
                return CheckProgress(fileName + ext, LAST_ADD);
            }
            Stream ms = GetMemoryStream(start, fileName, ext);
            if (start >= 0)
            {
                using ZipArchive zipArchive = new(ms, ZipArchiveMode.Update, true);
                IQueryable<Organization> orgs = dbContext.Organizations.Where(
                        o => o.Id == project.OrganizationId
                    );
               // IQueryable<Project> noteproject = dbContext.Projects.Join(orgs, p=>p.Id, o => o.NoteProjectId, (p, o) => p);
                
                IQueryable<Intellectualproperty>? ip = OrgIPs(orgs);
                if (start == 0)
                {
                    Dictionary<string, string> fonts = new()
                    {
                        { "Charis SIL", "" }
                    };
                    DateTime exported = AddCheckEntry(
                        zipArchive,
                        dbContext.Currentversions.FirstOrDefault()?.SchemaVersion ?? 6
                    );
                    AddJsonEntry(
                        zipArchive,
                        Tables.ActivityStates,
                        dbContext.Activitystates.ToList()
                    );
                    AddJsonEntry(zipArchive, Tables.Integrations, dbContext.Integrations.ToList());
                    AddJsonEntry(zipArchive, Tables.PassageTypes, dbContext.Passagetypes.ToList());
                    AddJsonEntry(zipArchive, Tables.PlanTypes, dbContext.Plantypes.ToList());
                    AddJsonEntry(zipArchive, Tables.ProjectTypes, dbContext.Projecttypes.ToList());
                    AddJsonEntry(zipArchive, Tables.Roles, dbContext.Roles.ToList());
                    AddJsonEntry(
                        zipArchive,
                        Tables.WorkflowSteps,
                        dbContext.Workflowsteps.ToList()
                    );
                    //org
                    List<Organization> orgList = orgs.ToList();

                    AddOrgLogos(zipArchive, orgList);
                    AddJsonEntry(zipArchive, Tables.Organizations, orgList);

                    //groups
                    IQueryable<Group> groups = dbContext.GroupsData.Join(
                        orgs,
                        g => g.OwnerId,
                        o => o.Id,
                        (g, o) => g
                    );
                    List<Groupmembership> gms = groups
                        .Join(
                            dbContext.Groupmemberships,
                            g => g.Id,
                            gm => gm.GroupId,
                            (g, gm) => gm
                        )
                        .Where(gm => !gm.Archived)
                        .ToList();
                    IEnumerable<User> users = gms.Join(
                            dbContext.Users,
                            gm => gm.UserId,
                            u => u.Id,
                            (gm, u) => u
                        )
                        .Where(x => !x.Archived);

                    foreach (string? font in gms.Where(gm => gm.Font != null).Select(gm => gm.Font))
                    {
                        if (font != null)
                            fonts [font] = ""; //add it if it's not there
                    }
                    foreach (
                        string? font in projects
                            .Where(p => p.DefaultFont != null)
                            .Select(p => p.DefaultFont)
                    )
                    {
                        if (font != null)
                            fonts [font] = ""; //add it if it's not there
                    }
                    AddFonts(zipArchive, fonts.Keys);
                    //users
                    List<User> userList = users.ToList();
                    AddUserAvatars(zipArchive, userList);

                    AddJsonEntry(
                        zipArchive,
                        Tables.IntellectualPropertys,
                        ip.ToList()
                    );
                    AddJsonEntry(
                        zipArchive,
                        Tables.Groups,
                        groups.Where(g => !g.Archived).ToList()
                    );
                    //groupmemberships
                    AddJsonEntry(zipArchive, Tables.GroupMemberships, gms);
                    AddJsonEntry(zipArchive, Tables.Users, userList);

                    //organizationmemberships
                    IEnumerable<Organizationmembership> orgmems = users
                        .Join(
                            dbContext.Organizationmemberships,
                            u => u.Id,
                            om => om.UserId,
                            (u, om) => om
                        )
                        .Where(om => om.OrganizationId == project.OrganizationId && !om.Archived);
                    AddJsonEntry(zipArchive, Tables.OrganizationMemberships, orgmems.ToList());

                    //projects
                    AddJsonEntry(zipArchive, Tables.Projects, projects.ToList());//.Concat(supportingProjects).ToList());
                    startNext = 1;
                }
                do //give me something to break out of
                {
                    if (
                        !CheckAdd(
                            1,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.ProjectIntegrations,
                            projects
                                .Join(
                                    dbContext.Projectintegrations,
                                    p => p.Id,
                                    pi => pi.ProjectId,
                                    (p, pi) => pi
                                )
                                .Where(x => !x.Archived)
                                .ToList()
                        )
                    )
                        break;
                    //plans
                    IQueryable<Plan> plans = projects
                        .Join(dbContext.Plans, p => p.Id, pl => pl.ProjectId, (p, pl) => pl)
                        .Where(x => !x.Archived);
                    /* NEXT RELEASE!
                    IQueryable<Plan> supportingPlans = noteproject
                        .Join(dbContext.Plans, p => p.Id, pl => pl.ProjectId, (p, pl) => pl)
                        .Where(x => !x.Archived);
                    */
                    if (
                        !CheckAdd(
                            2,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Plans,
                            plans/*.Concat(supportingPlans)*/.ToList()
                        )
                    )
                        break;
                    //sections
                    IQueryable<Section> sections = plans
                        .Join(dbContext.Sections, p => p.Id, s => s.PlanId, (p, s) => s)
                        .Where(x => !x.Archived);
                    IQueryable<Passage> passages = sections
                        .Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p)
                        .Where(x => !x.Archived);
                    //TODO did I get notes in the shared resources?
                    if (
                        !CheckAdd(
                            3,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Sections,
                            sections/*.Concat(supportingSections)*/.ToList()
                        )
                    )
                        break;
                    //passages
                    /* NEXT RELEASE!
                    IQueryable<Passage> supportingpassages = supportingSections
                        .Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p)
                        .Where(x => !x.Archived);
                    */
                    if (
                        !CheckAdd(
                            4,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Passages,
                            passages/*.Concat(supportingpassages)*/.ToList()
                        )
                    )
                        break;
                    //passagestatechanges
                    IQueryable<Passagestatechange> passagestatechanges = passages.Join(
                        dbContext.Passagestatechanges,
                        p => p.Id,
                        psc => psc.PassageId,
                        (p, psc) => psc
                    );
                    if (
                        !CheckAdd(
                            5,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.PassageStateChanges,
                            passagestatechanges.ToList()
                        )
                    )
                        break;
 
                    startNext++; //TODO REMOVE? instead of passagenotes

                    //mediafiles
                    //I need my mediafiles plus any shared resource mediafiles
                    IQueryable<Sectionresource> sectionresources = SectionResources(sections);

                    IEnumerable<Mediafile> sourcemediafiles = PlanSourceMedia(sectionresources);
                    IQueryable<Mediafile>? myMedia = PlanMedia(plans, ip);//NEXT RELEASE!, supportingpassages);

                    if (
                        !CheckAdd(
                            7,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Mediafiles,
                            myMedia.ToList().Union(sourcemediafiles.ToList()).OrderBy(m => m.Id).ToList()
                        )
                    )
                        break;

                    if (
                        !CheckAdd(
                            8,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.ArtifactCategorys,
                            dbContext.Artifactcategorys
                                .Where(a =>
                                        (
                                            a.OrganizationId == null
                                            || a.OrganizationId == project.OrganizationId
                                        ) && !a.Archived
                                )
                                .ToList()
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            9,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.ArtifactTypes,
                            dbContext.Artifacttypes
                                .Where(a =>
                                        (
                                            a.OrganizationId == null
                                            || a.OrganizationId == project.OrganizationId
                                        ) && !a.Archived
                                )
                                .ToList()
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            10,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.OrgWorkflowSteps,
                            dbContext.OrgworkflowstepsData
                                .Where(
                                    a => (a.OrganizationId == project.OrganizationId) && !a.Archived
                                )
                                .ToList()
                        )
                    )
                    break;
                    //only limit vernacular to those with passageids
                    IQueryable<Mediafile> attachedmediafiles = AttachedMedia(myMedia);
                    //ignore media from other plans (shared, notes etc)
                    IQueryable<Discussion> discussions = PlanDiscussions(myMedia.Join(plans, m => m.PlanId, p => p.Id, (m, p) => m)
                                );
                    if (
                        !CheckAdd(
                            11,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Discussions,
                            discussions.ToList()
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            12,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Comments,
                            dbContext.Comments
                                .Join(discussions, c => c.DiscussionId, d => d.Id, (c, d) => c)
                                .Where(x => !x.Archived)
                                .ToList()
                        )
                    )
                        break;

                    if (
                        !CheckAdd(
                            13,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.SectionResources,
                            sectionresources.ToList()
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            14,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.SectionResourceUsers,
                            sectionresources
                                .Join(
                                    dbContext.Sectionresourceusers,
                                    r => r.Id,
                                    u => u.SectionResourceId,
                                    (r, u) => u
                                )
                                .Where(x => !x.Archived)
                                .ToList()
                        )
                    )
                        break;

                    IQueryable<Orgkeyterm>? orgkeyterms = dbContext.OrgKeytermsData
                                    .Where(
                                        a => (a.OrganizationId == project.OrganizationId) && !a.Archived
                                    );
                    if (
                        !CheckAdd(
                            15,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.OrgKeyTerms,
                            orgkeyterms.ToList()
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            16,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.OrgKeyTermReferences,
                            dbContext.OrgKeytermReferencesData
                                .Join(orgkeyterms, r => r.OrgkeytermId, k => k.Id, (r, k) => r)
                                .ToList()
                        )
                        )
                        break;
                    if (
                        !CheckAdd(
                            17,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.OrgKeyTermTargets,
                            dbContext.OrgKeytermTargetsData.Where(
                                    a => (a.OrganizationId == project.OrganizationId) && !a.Archived
                                )
                                .ToList()
                        )
                    )
                        break;
                    IQueryable<Sharedresource>? sharedresources = dbContext.SharedresourcesData
                                    .Where(a => !a.Archived);
                    if (
                        !CheckAdd(
                            18,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.SharedResources,
                            sharedresources.ToList()
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            19,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.SharedResourceReferences,
                            dbContext.SharedresourcereferencesData
                                .Join(sharedresources, r => r.SharedResourceId, k => k.Id, (r, k) => r)
                                .ToList()
                        )
                    )
                        break;

                    //Now I need the media list of just those files to download...
                    //pick just the highest version media per passage (vernacular only) for eaf (TODO: what about bt?!)
                    //IQueryable<Mediafile> vernmediafiles =
                    //    from m in attachedmediafiles
                    //    where m.ArtifactTypeId == null
                    //    group m by m.PassageId into grp
                    //    select grp.OrderByDescending(m => m.VersionNumber).FirstOrDefault();

                    //if (!AddMediaEaf(20, dtBail, ref startNext, zipArchive, vernmediafiles.ToList(), null))
                    //    break;
                    startNext++; //instead of eaf
                    List<Mediafile> mediaList  = attachedmediafiles.ToList().Union(sourcemediafiles.ToList()).ToList();
                    AddAttachedMedia(zipArchive, mediaList, null);
                } while (false);
            }
            Fileresponse response = WriteMemoryStream(ms, fileName, startNext, ext);
            if (startNext == LAST_ADD + 1)
            {   //add the mediafiles
                string id= _SQSService.SendExportMessage(project.Id, ExportFolder, fileName + ext, 0);
            }
            return response;
        }

        public Fileresponse ImportFileURL(string sFile)
        {
            string extension = Path.GetExtension(sFile);
            string ContentType = "application/" + extension[1..];
            // Project project = dbContext.Projects.Where(p => p.Id == id).First();
            string fileName = string.Format(
                "{0}_{1}{2}",
                CleanFileName(Path.GetFileNameWithoutExtension(sFile)),
                DateTime.Now.Ticks,
                extension
            );
            //get a signed url for it now
            return new Fileresponse()
            {
                Message = fileName,
                FileURL = _S3Service.SignedUrlForPut(fileName, ImportFolder, ContentType).Message,
                Status = System.Net.HttpStatusCode.OK,
                ContentType = ContentType,
            };
        }

        private string ProjectDeletedReport(Project project)
        {
            return ChangesReport("project", "\"deleted\"", Serialize(project));
        }

        private string Serialize<TResource>(TResource m) where TResource : class, IIdentifiable<int>
        {
            return SerializerHelpers.ResourceToJson(
                m,
                _resourceGraph,
                _options,
                _resourceDefinitionAccessor,
                _metaBuilder
            );
        }

        private static string ChangesReport(string type, string online, string imported)
        {
            return "{\"type\":\""
                + type
                + "\", \"online\": "
                + online
                + ", \"imported\": "
                + imported
                + "}";
        }

        private string UserChangesReport(User online, User imported)
        {
            return ChangesReport(Tables.ToType(Tables.Users), Serialize(online), Serialize(imported));
        }

        private string SectionChangesReport(Section online, Section imported)
        {
            return (online.EditorId != imported.EditorId && online.EditorId != null)
                || (online.TranscriberId != imported.TranscriberId && online.TranscriberId != null)
                || online.State != imported.State
                ? ChangesReport(Tables.ToType(Tables.Sections), Serialize(online), Serialize(imported))
                : "";
        }

        private string PassageChangesReport(Passage online, Passage imported)
        {
            return online.StepComplete != imported.StepComplete
                ? ChangesReport(Tables.ToType(Tables.Passages), Serialize(online), Serialize(imported))
                : "";
        }

        private string MediafileChangesReport(Mediafile online, Mediafile imported)
        {
            if (online.Transcription != (imported.Transcription ?? online.Transcription) && online.Transcription != null)
            {
                Mediafile copy = (Mediafile)online.ShallowCopy();
                copy.AudioUrl = "";
                return ChangesReport(Tables.ToType(Tables.Mediafiles), Serialize(copy), Serialize(imported));
            }
            return "";
        }

        private string DiscussionChangesReport(Discussion online, Discussion imported)
        {
            return online.ArtifactCategoryId != imported.ArtifactCategoryId
                || online.GroupId != imported.GroupId
                || online.Resolved != imported.Resolved
                ? ChangesReport(Tables.ToType(Tables.Discussions), Serialize(online), Serialize(imported))
                : "";
        }

        private string CommentChangesReport(Comment online, Comment imported)
        {
            return online.CommentText != imported.CommentText
                || online.MediafileId != imported.MediafileId
                ? ChangesReport(Tables.ToType(Tables.Comments), Serialize(online), Serialize(imported))
                : "";
        }

        private string GrpMemChangesReport(Groupmembership online, Groupmembership imported)
        {
            return online.FontSize != imported.FontSize
                ? ChangesReport(Tables.ToType(Tables.GroupMemberships), Serialize(online), Serialize(imported))
                : "";
        }
        private static Fileresponse FileNotFound(string sFile)
        {
            return new Fileresponse()
            {
                Message = "File not found",
                FileURL = sFile,
                Status = HttpStatusCode.NotFound
            };
        }
        public async Task<Fileresponse> ImportSyncFileAsync(string sFile, int fileIndex, int start)
        {
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            S3Response response = await _S3Service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return FileNotFound(sFile);
            ZipArchive archive = new(response.FileStream);
            List<string> report = new();
            List<string> errors = new();
            string startIndex = "0/0";
            for (int ix = fileIndex; ix < archive.Entries.Count; ix++)
            {
                ZipArchiveEntry entry = archive.Entries[ix];
                ZipArchive zipEntry = new(entry.Open());
                Fileresponse fr = await ProcessImportFileAsync(zipEntry, 0, entry.Name, start, dtBail);
                if (fr.Status is HttpStatusCode.OK or HttpStatusCode.PartialContent)
                { //remove beginning and ending brackets
                    string msg = fr.Message.StartsWith("[")
                        ? fr.Message[1..^1]  //.Substring(1, fr.Message.Length - 2)
                        : fr.Message;
                    report.Add(msg);
                }
                else
                {
                    errors.Add(JsonSerializer.Serialize(fr));
                }
                if (fr.Status == HttpStatusCode.PartialContent)
                {
                    startIndex = string.Format("{0}/{1}", fileIndex, fr.Startindex);
                    break;
                }
            }
            _ = report.RemoveAll(s => s.Length == 0);
            _ = errors.RemoveAll(s => s.Length == 0);
            return errors.Count > 0
                ? ErrorResponse(
                    "{\"errors\": ["
                        + string.Join(",", errors)
                        + "], \"report\": ["
                        + string.Join(", ", report)
                        + "]}",
                    sFile
                )
                : new Fileresponse()
                {
                    Message = "[" + string.Join(",", report) + "]",
                    FileURL = sFile,
                    Status = startIndex == "0/0" ? HttpStatusCode.OK : HttpStatusCode.PartialContent,
                    ContentType = "application/itf",
                    Startindex = startIndex
                };
        }

        public async Task<Fileresponse> ImportFileAsync(int projectId, string sFile, int start)
        {
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            S3Response response = await _S3Service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return FileNotFound(sFile);
            ZipArchive archive = new(response.FileStream);
            return await ProcessImportFileAsync(archive, projectId, sFile, start, dtBail);
        }
        public async Task<Fileresponse> ImportCopyFileAsync(bool neworg, string sFile)
        {
            S3Response response = await _S3Service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return FileNotFound(sFile);
            ZipArchive archive = new(response.FileStream);
            return await ProcessImportCopyFileAsync(archive, neworg, sFile);
        }
        public async Task<Fileresponse> ImportCopyProjectAsync(bool neworg, int projectId, int start, int? newProjId)
        {
            Project? sourceproject = dbContext.Projects.FirstOrDefault(p => p.Id==projectId);
            return sourceproject == null
                ? ErrorResponse("Project not found", projectId.ToString())
                : await ProcessImportCopyProjectAsync(sourceproject, neworg, start, newProjId);
        }

        private static Fileresponse ProjectDeletedResponse(string msg, string sFile)
        {
            return ErrorResponse(msg, sFile, System.Net.HttpStatusCode.MovedPermanently);
        }

        private static Fileresponse NotCurrentProjectResponse(string msg, string sFile)
        {
            return ErrorResponse(msg, sFile, System.Net.HttpStatusCode.NotAcceptable);
        }

        private static Fileresponse ErrorResponse(
            string msg,
            string sFile,
            HttpStatusCode status = System.Net.HttpStatusCode.UnprocessableEntity
        )
        {
            const string ContentType = "application/itf";
            return new Fileresponse()
            {
                Message = msg,
                FileURL = sFile,
                Status = status,
                ContentType = ContentType,
                Startindex = ""
            };
        }

        private async Task<bool> CopyMediafile(Mediafile source, Mediafile? target)
        {
            if (source.S3File != null && target?.S3File != null)
            {
                S3Response response = await _S3Service.CopyFile(source.S3File, target.S3File, mediaService.DirectoryName(source), mediaService.DirectoryName(target));
                if (response.Status == HttpStatusCode.OK)
                {
                    target.AudioUrl = _S3Service.SignedUrlForGet(
                                    target.S3File ?? "",
                                    mediaService.DirectoryName(target),
                                    target.ContentType ?? ""
                                )
                                .Message;
                    return true;
                }
            }
            return false;
        }

        private async Task CopyMediaFile(Mediafile m, ZipArchive archive)
        {
            ZipArchiveEntry? f = archive.Entries
                .Where(e => e.Name == m.OriginalFile)
                .FirstOrDefault();
            if (f != null && m.S3File != null)
            {
                using Stream s = f.Open();
                using MemoryStream ms = new();
                s.CopyTo(ms);
                ms.Position = 0; // rewind
                S3Response response = await _S3Service.UploadFileAsync(
                    ms,
                    true,
                    m.ContentType ?? "",
                    m.S3File ?? "",
                    mediaService.DirectoryName(m)
                );
                m.AudioUrl = _S3Service
                    .SignedUrlForGet(
                        m.S3File ?? "",
                        mediaService.DirectoryName(m),
                        m.ContentType ?? ""
                    )
                    .Message;
            }
        }

        private void UpdateOfflineIds()
        {
            /* fix comment ids */
            List<Comment> comments = dbContext.Comments.Where(
                c => c.OfflineMediafileId != null
            ).ToList();
            foreach (Comment c in comments)
            {
                Mediafile? mediafile = dbContext.Mediafiles
                    .Where(m => m.OfflineId == c.OfflineMediafileId)
                    .FirstOrDefault();
                if (mediafile != null)
                {
                    c.OfflineMediafileId = null;
                    c.MediafileId = mediafile.Id;
                    c.LastModifiedOrigin = "electron";
                    c.DateUpdated = DateTime.UtcNow;
                }
            }
            dbContext.Comments.UpdateRange(comments);
            comments = dbContext.Comments.Where(
                c => c.DiscussionId == null && c.OfflineDiscussionId != null
            ).ToList();
            foreach (Comment c in comments)
            {
                Discussion? discussion = dbContext.Discussions
                    .Where(m => m.OfflineId == c.OfflineDiscussionId)
                    .FirstOrDefault();
                if (discussion != null)
                {
                    c.DiscussionId = discussion.Id;
                    c.LastModifiedOrigin = "electron";
                    c.DateUpdated = DateTime.UtcNow;
                }
            }
            dbContext.Comments.UpdateRange(comments);
            /* fix discussion ids */
            List<Discussion> discussions = dbContext.Discussions.Where(
                d => d.MediafileId == null && d.OfflineMediafileId != null
            ).ToList();
            foreach (Discussion d in discussions)
            {
                Mediafile? mediafile = dbContext.Mediafiles
                    .Where(m => m.OfflineId == d.OfflineMediafileId)
                    .FirstOrDefault();
                if (mediafile != null)
                {
                    d.MediafileId = mediafile.Id;
                    d.LastModifiedOrigin = "electron";
                    d.DateUpdated = DateTime.UtcNow;
                }
            }
            dbContext.Discussions.UpdateRange(discussions);

            List<Mediafile> mediafiles = dbContext.Mediafiles.Where(
                c => c.SourceMediaId == null && c.SourceMediaOfflineId != null
            ).ToList();
            foreach (Mediafile m in mediafiles)
            {
                Mediafile? sourcemedia = dbContext.Mediafiles
                    .Where(sm => sm.OfflineId == m.SourceMediaOfflineId)
                    .FirstOrDefault();
                if (sourcemedia != null)
                {
                    m.SourceMediaId = sourcemedia.Id;
                    m.LastModifiedOrigin = "electron";
                    m.DateUpdated = DateTime.UtcNow;
                }
            }
            dbContext.Mediafiles.UpdateRange(mediafiles);
            List<Intellectualproperty> ips = dbContext.IntellectualPropertys.Where(
                c => c.OfflineMediafileId != null
            ).ToList();
            foreach (Intellectualproperty c in ips)
            {
                Mediafile? mediafile = dbContext.Mediafiles
                    .Where(m => m.OfflineId == c.OfflineMediafileId)
                    .FirstOrDefault();
                if (mediafile != null)
                {
                    c.ReleaseMediafileId = mediafile.Id;
                    c.LastModifiedOrigin = "electron";
                    c.DateUpdated = DateTime.UtcNow;
                }
            }
            dbContext.IntellectualPropertys.UpdateRange(ips);
            /* fix Orgkeytermtarget ids */
            List<Orgkeytermtarget> ktts = dbContext.Orgkeytermtargets.Where(
                c => c.OfflineMediafileId != null
            ).ToList();
            foreach (Orgkeytermtarget c in ktts)
            {
                Mediafile? mediafile = dbContext.Mediafiles
                    .Where(m => m.OfflineId == c.OfflineMediafileId)
                    .FirstOrDefault();
                if (mediafile != null)
                {
                    c.OfflineMediafileId = null;
                    c.MediafileId = mediafile.Id;
                    c.LastModifiedOrigin = "electron";
                    c.DateUpdated = DateTime.UtcNow;
                }
            }
            dbContext.Orgkeytermtargets.UpdateRange(ktts);
            _ = dbContext.SaveChanges();
        }

        private int CompareMediafilesByArtifactTypeVersionDate(Mediafile a, Mediafile b)
        {
            int compareType = Nullable.Compare(a.ArtifactTypeId, b.ArtifactTypeId);
            return compareType != 0
                ? compareType
                : a.ArtifactTypeId == null ?
                Nullable.Compare(a.VersionNumber, b.VersionNumber) :
                Nullable.Compare(a.DateUpdated,b.DateUpdated);
        }
        private int? ValidArtifactCategory(int? categoryid) { return dbContext.Artifactcategorys.FirstOrDefault(c => c.Id == categoryid)?.Archived ?? true ? null : categoryid == 0 ? 1 : categoryid; }

        private void UpdateDiscussion(
            Discussion existing,
            Discussion importing,
            DateTime sourceDate,
            List<string> report
        )
        {
            if (!existing.Archived &&
                (existing.Subject != importing.Subject
                || existing.MediafileId != importing.MediafileId
                || existing.ArtifactCategoryId != importing.ArtifactCategoryId
                || existing.Resolved != importing.Resolved
                || existing.GroupId != importing.GroupId
                || existing.UserId != importing.UserId)
            )
            {
                if (existing.DateUpdated > sourceDate)
                    report.Add(DiscussionChangesReport(existing, importing));
                existing.Subject = importing.Subject;
                existing.ArtifactCategoryId = ValidArtifactCategory(importing.ArtifactCategoryId);
                existing.GroupId = importing.GroupId;
                existing.Resolved = importing.Resolved;
                existing.UserId = importing.UserId;
                existing.MediafileId = importing.MediafileId;
                existing.OfflineMediafileId = importing.OfflineMediafileId;
                existing.LastModifiedBy = importing.LastModifiedBy;
                existing.LastModifiedByUser = importing.LastModifiedByUser;
                existing.DateUpdated = DateTime.UtcNow;
                _ = dbContext.Discussions.Update(existing);
            }
        }

        private void UpdateComment(
            Comment existing,
            Comment importing,
            DateTime sourceDate,
            List<string> report
        )
        {

            if (
                existing.CommentText != importing.CommentText
                || existing.MediafileId != importing.MediafileId
                 || existing.Visible != importing.Visible
            )
            {
                if (existing.DateUpdated > sourceDate)
                    report.Add(CommentChangesReport(existing, importing));
                existing.CommentText = importing.CommentText;
                existing.MediafileId = importing.MediafileId;
                existing.OfflineMediafileId = importing.OfflineMediafileId;
                existing.Visible = importing.Visible;
                existing.DateUpdated = DateTime.UtcNow;
                existing.Archived = false;
                existing.LastModifiedByUser = importing.LastModifiedByUser;
                _ = dbContext.Comments.Update(existing);
            }
        }

        private void UpdateMediafile(
            Mediafile existing,
            Mediafile importing,
            DateTime sourceDate,
            List<string> report
        )
        {
            if (
                !existing.Archived
                && (
                    existing.Transcription != importing.Transcription
                    || existing.Transcriptionstate != importing.Transcriptionstate
                    || existing.Segments != importing.Segments
                    || existing.SourceMediaId != importing.SourceMediaId
                    || existing.SourceMediaOfflineId != importing.SourceMediaOfflineId
                    || existing.SourceSegments != importing.SourceSegments
                    || existing.VersionNumber != importing.VersionNumber
                    || existing.Topic != importing.Topic
                )
            )
            {
                if (existing.DateUpdated > sourceDate)
                    report.Add(MediafileChangesReport(existing, importing));
                existing.Link = importing.Link != null ? importing.Link : false;
                existing.Position = importing.Position;
                existing.RecordedbyUser = importing.RecordedbyUser;
                existing.PerformedBy = importing.PerformedBy;
                existing.Segments = importing.Segments;
                existing.SourceSegments = importing.SourceSegments;
                existing.SourceMediaOfflineId = importing.SourceMediaOfflineId;
                existing.Topic = importing.Topic;
                existing.Transcription = importing.Transcription ?? existing.Transcription;
                if (importing.Transcriptionstate != null) //from old desktop
                    existing.Transcriptionstate = importing.Transcriptionstate;
                existing.LastModifiedBy = importing.LastModifiedBy;
                existing.LastModifiedByUser = importing.LastModifiedByUser;
                existing.VersionNumber = importing.VersionNumber;
                existing.DateUpdated = DateTime.UtcNow;
                _ = dbContext.Mediafiles.Update(existing);
            }
        }

        private TResource ResourceObjectToResource<TResource>(ResourceObject ro, TResource s)
            where TResource : class, IIdentifiable
        {
            ResourceType resourceType = _resourceGraph.GetResourceType(typeof(TResource));
            IReadOnlyCollection<AttrAttribute>? attrs = resourceType.Attributes;
            IReadOnlyCollection<RelationshipAttribute>? rels = resourceType.Relationships;

            s.StringId = ro.Id;
            if (ro.Attributes != null)
                foreach (KeyValuePair<string, object?> row in ro.Attributes)
                {
                    AttrAttribute? myTypeAttribute = attrs.FirstOrDefault(
                        a => a.PublicName == row.Key
                    );
                    if (myTypeAttribute != null && row.Value != null && myTypeAttribute.Property.CanWrite)
                    {
                        object? value = ((JsonElement)row.Value).Deserialize(
                            myTypeAttribute.Property.PropertyType
                        );
                        if (value is DateTime)
                            value = ((DateTime)value).SetKindUtc();

                        myTypeAttribute.SetValue(s, value);
                    }
                }
            if (ro.Relationships != null)
                foreach (
                    KeyValuePair<string, RelationshipObject?> row in ro.Relationships.Where(
                        rel => rel.Value?.Data.SingleValue != null
                    )
                )
                {
                    RelationshipAttribute? myTypeRelationship = rels.FirstOrDefault(
                        r => r.PublicName == row.Key
                    );
                    if (
                        myTypeRelationship != null
                        && int.TryParse(row.Value?.Data.SingleValue?.Id, out int id)
                    )
                    {
                        object? p = dbContext.Find(myTypeRelationship.Property.PropertyType, id);
                        myTypeRelationship.SetValue(s, p);
                        AttrAttribute? myIdAttribute = attrs.FirstOrDefault(
                        a => a.PublicName == myTypeRelationship.PublicName + "-id"
                    );
                        if (myIdAttribute == null && myTypeRelationship.PublicName == "last-modified-by-user")
                            myIdAttribute = attrs.FirstOrDefault(a => a.PublicName == "last-modified-by");
                        if (myIdAttribute != null)
                            myIdAttribute.SetValue(s, id);
                        else
                            Logger.LogWarning("unable to find id attribute for {r}", myTypeRelationship.PublicName);
                    }
                }
            return s;
        }

        private static string ProperCase(string snake)
        {
            while (snake.IndexOf("-") > 0)
            {
                int ix = snake.IndexOf("-");
                snake = string.Concat(
                    snake.AsSpan(0, ix),
                    snake.Substring(ix + 1, 1).ToUpper(),
                    snake.AsSpan(ix + 2)
                );
            }
            return string.Concat(snake [..1].ToUpper(), snake.AsSpan(1));
        }
        private Project? ReadFileProject(ZipArchive archive)
        {
            IJsonApiOptions options = new JsonApiOptions();
            ZipArchiveEntry? projectsEntry = archive.GetEntry("data/D_projects.json");
            if (projectsEntry == null)
                return null;
            string json = new StreamReader(projectsEntry.Open()).ReadToEnd();

            Document? doc = JsonSerializer.Deserialize<Document>(
                    json,
                    options.SerializerReadOptions
                );
            ResourceObject? fileproject = doc?.Data.SingleValue ?? (doc?.Data.ManyValue?[0]);

            return fileproject == null ? null : ResourceObjectToResource(fileproject, new Project());
        }
        private Project? GetFileProject(ZipArchive archive)
        {
            Project? fileproject = ReadFileProject(archive);
            return fileproject == null ? null : dbContext.Projects.Find(fileproject.Id);
        }
        private int UpdateUsers(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                User u = ResourceObjectToResource(ro, new User());
                User? user = dbContext.Users.Find(u.Id);
                if (
                    user != null
                    && !user.Archived
                    && user.DateUpdated != u.DateUpdated
                )
                {
                    if (
                        user.DateUpdated > sourceDate
                        && user.DateUpdated != u.DateUpdated
                    )
                        report.Add(UserChangesReport(user, u));

                    user.DigestPreference = u.DigestPreference;
                    user.FamilyName = u.FamilyName;
                    user.GivenName = u.GivenName;
                    user.Locale = u.Locale;
                    user.Name = u.Name;
                    user.NewsPreference = u.NewsPreference;
                    user.Phone = u.Phone;
                    user.Timezone = u.Timezone;
                    user.UILanguageBCP47 = u.UILanguageBCP47;
                    user.LastModifiedBy = u.LastModifiedBy;
                    user.LastModifiedByUser = u.LastModifiedByUser;
                    user.DateUpdated = DateTime.UtcNow;
                    /* TODO: figure out if the avatar needs uploading */
                    _ = dbContext.Users.Update(user);
                }
            }
            return -1;
        }
        private int UpdateSections(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Section s = ResourceObjectToResource(ro, new Section());
                Section? section = dbContext.Sections.Find(s.Id);
                if (section != null && !section.Archived)
                {
                    if (section.DateUpdated > sourceDate)
                        report.Add(SectionChangesReport(section, s));

                    section.EditorId = s.Editor?.Id;
                    section.TranscriberId = s.Transcriber?.Id;
                    section.State = s.State;
                    section.LastModifiedBy = s.LastModifiedByUser?.Id;
                    section.LastModifiedByUser = s.LastModifiedByUser;
                    section.DateUpdated = DateTime.UtcNow;
                    _ = dbContext.Sections.Update(section);
                }
            }
            return -1;
        }
        private int UpdatePassages(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail, int currentuser)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Passage p = ResourceObjectToResource(ro, new Passage());
                Passage? passage = dbContext.Passages.Find(p.Id);
                if (
                    passage != null
                    && !passage.Archived
                    && (
                        passage.StepComplete != p.StepComplete
                        || passage.State != p.State //we don't use passage state anymore
                    )
                )
                {
                    if (passage.DateUpdated > sourceDate)
                    {
                        report.Add(PassageChangesReport(passage, p));
                    }
                    passage.State = p.State; //backward compatibility
                    passage.StepComplete = p.StepComplete;
                    passage.LastModifiedBy = p.LastModifiedByUser?.Id;
                    passage.LastModifiedByUser = p.LastModifiedByUser;
                    passage.DateUpdated = DateTime.UtcNow;
                    _ = dbContext.Passages.Update(passage);
                    Passagestatechange psc =
                                            new()
                                            {
                                                Comments = "Imported", //TODO Localize
                                                DateCreated = passage.DateUpdated,
                                                DateUpdated = passage.DateUpdated,
                                                LastModifiedBy = currentuser,
                                                PassageId = passage.Id,
                                                State = passage.State ?? "",
                                            };
                    _ = dbContext.Passagestatechanges.Add(psc);
                }
            }
            return -1;
        }
        private int CreateOrUpdateDiscussions(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Discussion d = ResourceObjectToResource(ro, new Discussion());
                if (d.Id > 0)
                {
                    Discussion? discussion = dbContext.Discussions.Find(d.Id);
                    if (discussion != null)
                        UpdateDiscussion(discussion, d, sourceDate, report);
                }
                else
                {
                    //check if it's been uploaded another way (ie. itf and now we're itfs or vice versa)
                    Discussion? discussion = dbContext.Discussions
                                            .Where(x => x.OfflineId == d.OfflineId)
                                            .FirstOrDefault();
                    if (discussion == null)
                    {
                        _ = dbContext.Discussions.Add(
                            new Discussion
                            {
                                ArtifactCategoryId = ValidArtifactCategory(d.ArtifactCategoryId),
                                MediafileId = d.MediafileId,
                                OfflineId = d.OfflineId,
                                OfflineMediafileId = d.OfflineMediafileId,
                                OrgWorkflowStepId = d.OrgWorkflowStepId,
                                GroupId = d.Group?.Id,
                                Resolved = d.Resolved,
                                Segments = d.Segments,
                                Subject = d.Subject,
                                UserId = d.User?.Id,
                                LastModifiedBy = d.LastModifiedBy,
                                LastModifiedByUser = d.LastModifiedByUser,
                                DateCreated = d.DateCreated,
                                DateUpdated = DateTime.UtcNow,
                            }
                        );
                    }
                    else
                    {
                        UpdateDiscussion(discussion, d, sourceDate, report);
                    }
                }
            }
            return -1;
        }
        private int CreateOrUpdateComments(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Comment c = ResourceObjectToResource(ro, new Comment());
                if (c.Id > 0)
                {
                    Comment? comment = dbContext.Comments.Find(c.Id);
                    if (comment != null)
                        UpdateComment(comment, c, sourceDate, report);
                }
                else
                {
                    //check if it's been uploaded another way (ie. itf and now we're itfs or vice versa)
                    Comment? comment = dbContext.Comments
                        .Where(x => x.OfflineId == c.OfflineId)
                        .FirstOrDefault();
                    if (comment == null)
                    {
                        _ = dbContext.Comments.Add(
                            new Comment
                            {
                                OfflineId = c.OfflineId,
                                OfflineMediafileId = c.OfflineMediafileId,
                                OfflineDiscussionId = c.OfflineDiscussionId,
                                DiscussionId =
                                    c.DiscussionId == 0 ? null : c.DiscussionId,
                                CommentText = c.CommentText,
                                MediafileId = c.MediafileId,
                                LastModifiedBy = c.LastModifiedBy,
                                LastModifiedByUser = c.LastModifiedByUser,
                                DateCreated = c.DateCreated,
                                DateUpdated = DateTime.UtcNow,
                                Visible = c.Visible,
                            }
                        );
                        //mediafileid will be updated when mediafiles are processed if 0;
                    }
                    else
                        UpdateComment(comment, c, sourceDate, report);
                }
            }
            return -1;
        }
        private async Task<int> CreateOrUpdateMediafiles(IList<Mediafile> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail, ZipArchive archive)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail)
                    return lastIndex;
                Mediafile m = lst[lastIndex];
                Dictionary<int, int> passageVersions = new();
                Mediafile? mediafile;
                if (m.Id > 0)
                {
                    mediafile = dbContext.Mediafiles.Find(m.Id);
                    if (mediafile != null)
                        UpdateMediafile(mediafile, m, sourceDate, report);
                }
                else
                {
                    //check if it's been uploaded another way (ie. itf and now we're itfs or vice versa)
                    mediafile = dbContext.Mediafiles.FirstOrDefault(x => x.OfflineId == m.OfflineId);
                    if (mediafile == null)
                    {
                        if (!Convert.ToBoolean(m.VersionNumber))
                            m.VersionNumber = 1;

                        /* check the artifacttype */
                        if (m.PassageId != null && m.IsVernacular)
                        {
                            if (
                                passageVersions.TryGetValue(
                                    (int)m.PassageId,
                                    out int existingVersion
                                )
                            )
                            {
                                m.VersionNumber = existingVersion + 1;
                            }
                            else
                            {
                                mediafile = dbContext.Mediafiles
                                    .Where(p =>
                                            p.PassageId == m.PassageId
                                            && !p.Archived
                                            && p.ArtifactTypeId == null //IsVernacular
                                    )
                                    .OrderByDescending(p => p.VersionNumber)
                                    .FirstOrDefault();
                                if (mediafile != null)
                                    m.VersionNumber = mediafile.VersionNumber + 1;
                            }
                            passageVersions [(int)m.PassageId] = m.VersionNumber ?? 1;
                        }
                        m.S3File = await mediaService.GetNewFileNameAsync(m);
                        m.AudioUrl = _S3Service
                            .SignedUrlForPut(
                                m.S3File,
                                mediaService.DirectoryName(m),
                                m.ContentType ?? ""
                            )
                            .Message;
                        await CopyMediaFile(m, archive);
                        _ = dbContext.Mediafiles.Add(
                            new Mediafile
                            {
                                ArtifactTypeId = m.ArtifactTypeId,
                                ArtifactCategoryId = ValidArtifactCategory(m.ArtifactCategoryId),
                                AudioUrl = m.AudioUrl,
                                ContentType = m.ContentType,
                                Duration = m.Duration,
                                Filesize = m.Filesize,
                                OriginalFile = m.OriginalFile,
                                Languagebcp47 = m.Languagebcp47,
                                //LastModifiedByUserId = m.LastModifiedByUserId,
                                Link = m.Link != null ? m.Link : false,
                                OfflineId = m.OfflineId,
                                PassageId = m.PassageId,
                                PerformedBy = m.PerformedBy,
                                PlanId = m.PlanId,
                                Position = m.Position,
                                ReadyToShare = m.ReadyToShare,
                                // RecordedbyuserId = m.RecordedbyuserId,
                                RecordedbyUser = m.RecordedbyUser,
                                ResourcePassageId = m.ResourcePassageId,
                                S3File = m.S3File,
                                Segments = m.Segments,
                                Topic = m.Topic,
                                Transcription = m.Transcription,
                                Transcriptionstate = m.Transcriptionstate,
                                VersionNumber = m.VersionNumber,
                                SourceMediaId = m.SourceMediaId,
                                SourceMediaOfflineId = m.SourceMediaOfflineId,
                                SourceSegments = m.SourceSegments,
                                LastModifiedBy = m.LastModifiedBy,
                                LastModifiedByUser = m.LastModifiedByUser,
                                DateCreated = m.DateCreated,
                                DateUpdated = DateTime.UtcNow,
                            }
                        );
                        await dbContext.SaveChangesNoTimestampAsync();
                    }
                    else
                        UpdateMediafile(mediafile, m, sourceDate, report);
                }
            }
            return -1;
        }
        private int UpdateGroupMemberships(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];

                Groupmembership gm = ResourceObjectToResource(ro,new Groupmembership());
                Groupmembership? grpmem = dbContext.Groupmemberships.Find(gm.Id);
                if (
                    grpmem != null
                    && !grpmem.Archived
                    && grpmem.FontSize != gm.FontSize
                )
                {
                    if (grpmem.DateUpdated > sourceDate)
                        report.Add(GrpMemChangesReport(grpmem, gm));
                    grpmem.FontSize = gm.FontSize;
                    grpmem.LastModifiedBy = gm.LastModifiedBy;
                    grpmem.LastModifiedByUser = gm.LastModifiedByUser;
                    grpmem.DateUpdated = DateTime.UtcNow;
                    _ = dbContext.Groupmemberships.Update(grpmem);
                }
            }
            return -1;
        }
        private int CreatePassageStateChanges(IList<ResourceObject> lst, int startId, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];

                Passagestatechange psc = ResourceObjectToResource(ro, new Passagestatechange());
                //see if it's already there...
                IQueryable<Passagestatechange> dups =
                        dbContext.Passagestatechanges.Where(c =>
                            c.PassageId == psc.PassageId
                            && c.DateCreated == psc.DateCreated
                            && c.State == psc.State);
                if (!dups.Any())
                { /* if I send psc in directly, the id goes wonky...must be *something* different in the way it is initialized (tried setting id=0), so copy relevant info here */
                    _ = dbContext.Passagestatechanges.Add(
                        new Passagestatechange
                        {
                            PassageId = psc.PassageId,
                            State = psc.State,
                            DateCreated = psc.DateCreated,
                            Comments = psc.Comments,
                            LastModifiedBy = psc.LastModifiedBy,
                            LastModifiedByUser = psc.LastModifiedByUser,
                            DateUpdated = DateTime.UtcNow,
                        }
                    );
                }
            }
            return -1;
        }
        private int CreateIPs(IList<ResourceObject> lst, int startId, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];

                Intellectualproperty ip = ResourceObjectToResource(ro, new Intellectualproperty());
                if (ip.Id > 0)
                {
                    Intellectualproperty? existing = dbContext.IntellectualPropertys.Find(ip.Id);
                    if (existing != null)
                        continue; //do nothing no updateIP currently...
                }
                else
                {
                    //check if it's been uploaded another way (ie. itf and now we're itfs or vice versa)
                    Intellectualproperty? existing = dbContext.IntellectualPropertys
                                        .Where(x => x.OfflineId == ip.OfflineId)
                                        .FirstOrDefault();
                    if (existing == null)
                    {
                        _ = dbContext.IntellectualPropertys.Add(
                            new Intellectualproperty
                            {
                                RightsHolder = ip.RightsHolder,
                                Notes = ip.Notes,
                                OfflineMediafileId = ip.OfflineMediafileId,
                                OrganizationId = ip.Organization?.Id ?? 0,
                                OfflineId = ip.OfflineId,
                                ReleaseMediafileId = ip.ReleaseMediafile?.Id,
                                LastModifiedBy = ip.LastModifiedBy,
                                LastModifiedByUser = ip.LastModifiedByUser,
                                DateCreated = ip.DateCreated,
                                DateUpdated = DateTime.UtcNow,
                            }
                        );
                    }

                }
            }
            return -1;
        }
        private int CreateOrgKeyTermTargets(IList<ResourceObject> lst, int startId, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Orgkeytermtarget tt = ResourceObjectToResource(ro, new Orgkeytermtarget());
                if (tt.Id == 0)
                {
                    //check if it's been uploaded another way (ie. itf and now we're itfs or vice versa)
                    Orgkeytermtarget? ktt = dbContext.Orgkeytermtargets
                                        .Where(x => x.OfflineId == tt.OfflineId)
                                        .FirstOrDefault();
                    if (ktt == null)
                    {
                        _ = dbContext.Orgkeytermtargets.Add(
                            new Orgkeytermtarget
                            {
                                Organization = tt.Organization,
                                OrganizationId = tt.Organization?.Id ?? 0,
                                Term = tt.Term,
                                Target = tt.Target,
                                TermIndex = tt.TermIndex,
                                OfflineId = tt.OfflineId,
                                OfflineMediafileId = tt.OfflineMediafileId,
                                MediafileId = tt.Mediafile?.Id, //this won't ever happen...
                                LastModifiedBy = tt.LastModifiedBy,
                                LastModifiedByUser = tt.LastModifiedByUser,
                                DateCreated = tt.DateCreated,
                                DateUpdated = DateTime.UtcNow,
                            }
                        );
                        //mediafileid will be updated when mediafiles are processed if 0;
                    }
                }
            }
            return -1;
        }

        private async Task<Fileresponse> ProcessImportFileAsync(
            ZipArchive archive,
            int projectid,
            string sFile,
            int start,
            DateTime dtBail
        )
        {
            IJsonApiOptions options = new JsonApiOptions();
            List<string> report = new();
            List<string> deleted = new();
            DateTime? getsourceDate = CheckSILTranscriber(archive);
            if (getsourceDate == null)
                return ErrorResponse("SILTranscriber not present", sFile);
            //force it to a not nullable type
            DateTime sourceDate = getsourceDate ?? DateTime.Now;
            if (start == 0)
            {
                try
                {
                    ZipArchiveEntry? checkEntry = archive.GetEntry("SILTranscriberOffline");
                    //var exportTime = new StreamReader(checkEntry.Open()).ReadToEnd();
                }
                catch
                {
                    return ErrorResponse("SILTranscriberOffline not present", sFile);
                }

                //check project if provided
                Project? project;
                try
                {
                    project = GetFileProject(archive);

                    if (project == null)
                        return ErrorResponse("Project data not present", sFile);

                    if (projectid > 0)
                    {
                        if (projectid != project.Id)
                        {
                            return NotCurrentProjectResponse(
                                project.Name,
                                sFile
                            );
                        }
                    }
                    if (project.Archived)
                        return ProjectDeletedResponse(project.Name, sFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return ErrorResponse(
                        "Invalid ITF File - error finding project -" + ex.Message,
                        sFile
                    );
                }
            }
            try
            {
                int startId = -1;
                start = StartIndex.GetStart(start, ref startId);
                while (start < archive.Entries.Count)
                {
                    ZipArchiveEntry entry = archive.Entries[start];

                    if (!entry.FullName.StartsWith("data"))
                    {
                        start++;
                        continue;
                    }
                    string name = Path.GetFileNameWithoutExtension(entry.Name[2..]);
                    string? json = new StreamReader(entry.Open()).ReadToEnd();
                    Document? doc = JsonSerializer.Deserialize<Document>(
                        json,
                        options.SerializerReadOptions
                    );
                    IList<ResourceObject>? lst = doc?.Data.ManyValue;
                    if (doc == null || lst == null)
                    {
                        start++;
                        continue;
                    } 
                    switch (name)
                    {
                        case Tables.Users:
                            startId = UpdateUsers(lst, startId, sourceDate, report, dtBail);
                            break;

                        case Tables.Sections:
                            startId = UpdateSections(lst, startId, sourceDate, report, dtBail);
                            break;

                        case Tables.Passages:
                            int currentuser = CurrentUser()?.Id ?? 0;
                            startId = UpdatePassages(lst, startId, sourceDate, report, dtBail, currentuser);
                            break;

                        case Tables.Discussions:
                            startId = CreateOrUpdateDiscussions(lst, startId, sourceDate, report, dtBail);
                            break;

                        case Tables.Comments:
                            startId = CreateOrUpdateComments(lst, startId, sourceDate, report, dtBail);
                            break;

                        case Tables.Mediafiles:
                            List<Mediafile> sorted = new();
                            foreach (ResourceObject ro in lst)
                            {
                                sorted.Add(ResourceObjectToResource(ro, new Mediafile()));
                            }
                            sorted.Sort(CompareMediafilesByArtifactTypeVersionDate);
                            startId = await CreateOrUpdateMediafiles(sorted, startId, sourceDate, report, dtBail, archive);
                            break;

                        case Tables.GroupMemberships:
                            startId = UpdateGroupMemberships(lst, startId, sourceDate, report, dtBail);
                            break;

                        /*  Local changes to project integrations should just stay local
                        case "projectintegrations":
                            List<ProjectIntegration> pis = jsonApiDeSerializer.DeserializeList<ProjectIntegration>(data);
                            break;
                        */

                        case Tables.PassageStateChanges:
                            startId = CreatePassageStateChanges(lst, startId, dtBail);
                            break;

                        case Tables.IntellectualPropertys:
                            startId = CreateIPs(lst, startId,  dtBail);
                            break;

                        case Tables.OrgKeyTermTargets:
                            startId = CreateOrgKeyTermTargets(lst, startId, dtBail);
                            break;

                        default: 
                            startId = -1;
                            break;

                    }
                    start = StartIndex.SetStart(start, ref startId);
                };
                int ret = await dbContext.SaveChangesNoTimestampAsync();

                UpdateOfflineIds();
                
                _ = report.RemoveAll(s => s.Length == 0);

                return new Fileresponse()
                {
                    Message = "[" + string.Join(",", report) + "]",
                    FileURL = sFile,
                    Status = start == archive.Entries.Count ? HttpStatusCode.OK : HttpStatusCode.PartialContent,
                    ContentType = "application/itf",
                    Startindex = start == archive.Entries.Count ? "-1" : start.ToString(),
                };
            }
            catch (Exception ex)
            {
                return ErrorResponse(
                    ex.Message
                        + (
                            ex.InnerException != null && ex.InnerException.Message != ""
                                ? "=>" + ex.InnerException.Message
                                : ""
                        ),
                    sFile
                );
            }
        }
        private static DateTime? CheckSILTranscriber(ZipArchive archive)
        {
            try
            {
                ZipArchiveEntry? sourceEntry = archive.GetEntry("SILTranscriber");
                return sourceEntry == null ? null : Convert.ToDateTime(new StreamReader(sourceEntry.Open()).ReadToEnd());
            }
            catch
            {
                return null;
            }
        }
        private async Task<Organization> CreateNewOrg(Organization sourceOrg, bool sameName, User user)
        {
            int tryn = 1;
            string orgname = sourceOrg.Name+(sameName ? "" : "_c"+tryn++).ToString();
            while (dbContext.Organizations.FirstOrDefault(x => x.Name == orgname) != null)
            {
                orgname = sourceOrg.Name + "_c" + tryn++.ToString();
            }
            EntityEntry<Organization>? t = dbContext.Organizations.Add(
                new Organization
                {
                    Name = orgname,
                    PublicByDefault = true,
                    Description = sourceOrg.Description,
                    DefaultParams = sourceOrg.DefaultParams,
                    OwnerId = user.Id,
                });
            dbContext.SaveChanges();
            //get it again because the slug should be there...
            await dbContext.Entry(t.Entity).ReloadAsync();
            
            dbContext.Organizationmemberships.Add(
                new Organizationmembership
                {
                    OrganizationId = t.Entity.Id,
                    UserId = user.Id,
                    RoleId = dbContext.Roles.First(r => r.Rolename == RoleName.Admin && r.Orgrole).Id,
                });
            EntityEntry<Group>? g = dbContext.Groups.Add(new Group
            {
                Name = "All users of " + orgname,
                Abbreviation= "all-users",
                OwnerId= t.Entity.Id,
                AllUsers = true,
            });
            dbContext.SaveChanges();
            dbContext.Groupmemberships.Add(
                new Groupmembership
                {
                    GroupId = g.Entity.Id,
                    UserId = user.Id,
                    RoleId = dbContext.Roles.First(r => r.Rolename == RoleName.Admin && r.Grouprole).Id,
                });
            try
            {
                dbContext.SaveChanges();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return t.Entity;
        }
        private Project CreateNewProject(Project source, bool sameName, int orgId, User user)
        {
            int tryn = 1;
            string projname = source.Name+(sameName ? "" : "_c"+tryn++).ToString();
            while (dbContext.Projects.FirstOrDefault(x => x.OrganizationId == orgId && x.Name == projname) != null)
            {
                projname = source.Name + "_c" + tryn++.ToString();
            }
            dbContext.SaveChanges();
            Group allusers = dbContext.Groups.First(g => g.OwnerId == orgId && g.AllUsers && !g.Archived);
            EntityEntry<Project>? t = dbContext.Projects.Add(
                new Project
                {
                    Name = projname,
                    ProjecttypeId = source.ProjecttypeId,
                    Description = source.Description,
                    DefaultParams = source.DefaultParams,
                    OwnerId = user.Id,
                    OrganizationId= orgId,
                    Language= source.Language,
                    LanguageName= source.LanguageName,
                    IsPublic = source.IsPublic,
                    Uilanguagebcp47 = source.Uilanguagebcp47,
                    DefaultFont = source.DefaultFont,
                    DefaultFontSize = source.DefaultFontSize,
                    Rtl = source.Rtl,
                    GroupId = allusers.Id,
                    SpellCheck = source.SpellCheck,
                });
            dbContext.SaveChanges();

            return t.Entity;
        }
        private async Task<Plan> CreateNewPlan(Plan source, Project project, User user)
        {
            EntityEntry<Plan>? t = dbContext.Plans.Add(
                new Plan
                {
                    Name = project.Name,
                    PlantypeId = source.PlantypeId,
                    ProjectId = project.Id,
                    OwnerId = user.Id,
                    OrganizedBy = source.OrganizedBy,
                    Tags = source.Tags,
                    Flat = source.Flat,
                    SectionCount = source.SectionCount,
                });
            dbContext.SaveChanges();
            await dbContext.Entry(t.Entity).ReloadAsync();
            return t.Entity;
        }
        #region Copy
        private Dictionary<int, int> CopySections(IList<Section> lst, bool sameOrg, int planId, User currentuser)
        {
            Dictionary<int, Section> map = new();
            foreach (Section s in lst)
            {
                EntityEntry<Section>? t = dbContext.Sections.Add(
                    new Section
                    {
                        Name = s.Name,
                        PlanId = planId,
                        Sequencenum = s.Sequencenum,
                        EditorId = sameOrg ?  s.Editor?.Id : currentuser.Id,
                        TranscriberId = sameOrg ? s.Transcriber?.Id : currentuser.Id,
                        State = s.State,
                    });
                map.Add(s.Id, t.Entity);
            }
            dbContext.SaveChanges();
            Dictionary<int, int> result = new();
            foreach (KeyValuePair<int, Section> kvp in map)
                result.Add(kvp.Key, kvp.Value.Id);
            return result;
        }
        private Dictionary<int, int> CopyPassages(IList<Passage> lst,
            Dictionary<int, int> SectionMap, Dictionary<int, int>? OrgworkflowstepMap)
        {
            Dictionary<int, Passage> map = new();
            foreach (Passage p in lst)
            {
                EntityEntry<Passage>? t = dbContext.Passages.Add(
                    new Passage
                    {
                        Sequencenum = p.Sequencenum,
                        Book = p.Book,
                        Reference = p.Reference,
                        Hold = p.Hold,
                        Title = p.Title,
                        SectionId = SectionMap.GetValueOrDefault(p.SectionId),
                        StepComplete =  MapStepComplete(p.StepComplete, OrgworkflowstepMap),
                    });
                map.Add(p.Id, t.Entity);
            }
            dbContext.SaveChanges();
            Dictionary<int, int> result = new();
            foreach (KeyValuePair<int, Passage> kvp in map)
                result.Add(kvp.Key, kvp.Value.Id);
            return result;
        }
        private Dictionary<int, int> CopyArtifactCategorys(List<Artifactcategory> lst, int orgId)
        {
            Dictionary<int, Artifactcategory> map = new();
            foreach (Artifactcategory c in lst)
            {
                Artifactcategory? myc = dbContext.Artifactcategorys.FirstOrDefault(m => (m.OrganizationId == null || m.OrganizationId == orgId) && m.Categoryname == c.Categoryname && !m.Archived);
                if (myc == null)
                {
                    EntityEntry<Artifactcategory>? t = dbContext.Artifactcategorys.Add(
                        new Artifactcategory
                        {
                            OrganizationId= orgId,
                            Categoryname = c.Categoryname,
                            Discussion = c.Discussion,
                            Resource = c.Resource,
                        });
                    map.Add(c.Id, t.Entity);
                }
                else
                {
                    map.Add(c.Id, myc);
                }
            }
            dbContext.SaveChanges();
            Dictionary<int, int> result = new();
            foreach (KeyValuePair<int, Artifactcategory> kvp in map)
                result.Add(kvp.Key, kvp.Value.Id);
            return result;
        }
         private Dictionary<int, int> MapArtifactTypes(IList<ResourceObject> lst)
        {
            Dictionary<int, int> result = new();
            foreach (ResourceObject ro in lst)
            {
                Artifacttype a = ResourceObjectToResource(ro, new Artifacttype());

                Artifacttype? myc = dbContext.Artifacttypes.FirstOrDefault(m => m.OrganizationId == null && m.Typename == a.Typename && !m.Archived) ?? throw new Exception("missing type" + a.Typename);

                result.Add(a.Id, myc.Id);
            }
            return result;
        }

        private Dictionary<int, int> CopyOrgworkflowsteps(IList<Orgworkflowstep> lst, int orgId)
        {
            Dictionary<int, Orgworkflowstep> map = new();
            foreach (Orgworkflowstep s in lst)
            {
                Orgworkflowstep? ex = dbContext.Orgworkflowsteps.Where(o => o.OrganizationId == orgId && o.Name == s.Name).FirstOrDefault();
                if (ex != null)
                    map.Add(s.Id, ex);
                else
                {
                    EntityEntry<Orgworkflowstep>? t = dbContext.Orgworkflowsteps.Add(
                    new Orgworkflowstep
                    {
                        OrganizationId= orgId,
                        Process = s.Process,
                        Name = s.Name,
                        Sequencenum= s.Sequencenum,
                        Tool = s.Tool,
                        Permissions = s.Permissions,  //will this need to be adapted???  TODO
                    });
                    map.Add(s.Id, t.Entity);
                }
            }
            dbContext.SaveChanges();
            Dictionary<int, int> result = new();
            foreach (KeyValuePair<int, Orgworkflowstep> kvp in map)
                result.Add(kvp.Key, kvp.Value.Id);
            return result;
        }
        private Dictionary<int, int> FindOrgworkflowsteps(IList<Orgworkflowstep> lst, int orgId)
        {
            Dictionary<int, Orgworkflowstep> map = new();
            foreach (Orgworkflowstep s in lst)
            {
                Orgworkflowstep? ex = dbContext.Orgworkflowsteps.Where(o => o.OrganizationId == orgId && o.Name == s.Name).FirstOrDefault();
                if (ex != null)
                    map.Add(s.Id, ex);
                else
                {
                    EntityEntry<Orgworkflowstep>? t = dbContext.Orgworkflowsteps.Add(
                    new Orgworkflowstep
                    {
                        OrganizationId= orgId,
                        Process = s.Process,
                        Name = s.Name,
                        Sequencenum= s.Sequencenum,
                        Tool = s.Tool,
                        Permissions = s.Permissions,  //will this need to be adapted???  TODO
                    });
                    map.Add(s.Id, t.Entity);
                }   
            }
            dbContext.SaveChanges();
            Dictionary<int, int> result = new();
            foreach (KeyValuePair<int, Orgworkflowstep> kvp in map)
                result.Add(kvp.Key, kvp.Value.Id);
            return result;
        }
        private Dictionary<int, int> CopyOrgkeytermtargets(IList<Orgkeytermtarget> lst, int orgId, Dictionary<int,int> mediafileMap)
        {
            Dictionary<int, Orgkeytermtarget> map = new();
            foreach (Orgkeytermtarget s in lst)
            {
                EntityEntry<Orgkeytermtarget>? t = dbContext.Orgkeytermtargets.Add(
                    new Orgkeytermtarget
                    {
                        OrganizationId= orgId,
                        Term = s.Term,
                        TermIndex = s.TermIndex,
                        Target = s.Target,
                        MediafileId = mediafileMap.GetValueOrDefault(s.MediafileId?? 0)
                    });
                ;
                ;
                map.Add(s.Id, t.Entity);
            }
            dbContext.SaveChanges();
            Dictionary<int, int> result = new();
            foreach (KeyValuePair<int, Orgkeytermtarget> kvp in map)
                result.Add(kvp.Key, kvp.Value.Id);
            return result;
        }
        private void CopyIP(IList<Intellectualproperty> lst, int orgId, Dictionary<int, int> MediafileMap)
        {
            foreach (Intellectualproperty ip in lst)
            {
                _ = dbContext.IntellectualPropertys.Add(
                   new Intellectualproperty
                   {
                       RightsHolder = ip.RightsHolder,
                       Notes = ip.Notes,
                       OfflineMediafileId = "",
                       OrganizationId = orgId,
                       OfflineId = "",
                       ReleaseMediafileId = ip.ReleaseMediafileId == null ? null : MediafileMap.GetValueOrDefault(ip.ReleaseMediafileId ?? 0),
                   });

            }
        }
        private Dictionary<int, int> CopySectionResources(IList<Sectionresource> lst, int orgId, int projectId,
            Dictionary<int, int> sectionMap, Dictionary<int, int> mediafileMap,
            Dictionary<int, int>? owfsMap, Dictionary<int, int> passageMap)
        {
            Dictionary<int, Sectionresource> map = new();
            int internalize = dbContext.Orgworkflowsteps.ToList().Where(s => s.OrganizationId == orgId && s.Tool.Contains("{\"tool\": \"resource")).FirstOrDefault()?.Id ?? 0;
            foreach (Sectionresource sr in lst)
            {
                int stepId = owfsMap == null ? sr.OrgWorkflowStepId : owfsMap.GetValueOrDefault(sr.OrgWorkflowStepId);
                if (stepId == 0)
                    stepId = internalize;
                int? m = sr.MediafileId == null ? null : mediafileMap.GetValueOrDefault(sr.MediafileId ?? 0);
                if (sr.MediafileId != null && (m ?? 0) == 0)
                {
                    Console.WriteLine($"Mediafile {sr.MediafileId} not found");
                }
                else
                {
                    EntityEntry<Sectionresource>? t =  dbContext.Sectionresources.Add(
                       new Sectionresource
                       {
                           SequenceNum = sr.SequenceNum,
                           Description = sr.Description,
                           SectionId = sectionMap.GetValueOrDefault(sr.SectionId),
                           MediafileId = m,
                           OrgWorkflowStepId = stepId,
                           PassageId = sr.PassageId == null ? null : passageMap.GetValueOrDefault(sr.PassageId??0),
                           ProjectId = projectId
                       });
                    map.Add(sr.Id, t.Entity);
                }
            }
            dbContext.SaveChanges();
            Dictionary<int, int> result = new();
            foreach (KeyValuePair<int, Sectionresource> kvp in map)
                result.Add(kvp.Key, kvp.Value.Id);
            return result;
        }
        private void CopySectionResourceUsers(IList<Sectionresourceuser> lst, Dictionary<int, int> sectionResourceMap)
        {
            foreach (Sectionresourceuser sr in lst)
            {
                _ = dbContext.Sectionresourceusers.Add(
                   new Sectionresourceuser
                   {
                       SectionResourceId = sectionResourceMap.GetValueOrDefault(sr.SectionResourceId),
                       UserId = sr.UserId,
                   });
            }
        }
        private static string? MapStepComplete(string? source, Dictionary<int, int>? OrgworkflowstepMap)
        {
            if (source == null)
                return null;
            if (OrgworkflowstepMap == null) //same org
                return source;
            JObject result = JObject.Parse(source ?? "{}");
            JToken? js = result["completed"];
            if (js != null && js.Type == JTokenType.Array)
            {
                foreach (JToken entry in js.Children())
                {
                    if (int.TryParse(entry ["stepid"]?.ToString(), out int id))
                        entry ["stepid"] = OrgworkflowstepMap.GetValueOrDefault(id).ToString();
                }
                return result.ToString();
            }
            return null;
        }
        private async Task<Dictionary<int, Mediafile>> CopyMediafilesAsync(IList<Mediafile> lst, bool sameOrg, Plan plan,
            Dictionary<int, int> passageMap, Dictionary<int, int>? artifacttypeMap,
            Dictionary<int, int>? artifactcategoryMap, int start, int? newProjId, DateTime? dtBail)
        {
            Dictionary<int, Mediafile> map = new();
            string suffix = "_" + plan.Slug;
            for (int ix = start; ix < lst.Count && (dtBail == null || DateTime.Now < dtBail); ix++)
            {
                Mediafile m = lst[ix];
                int? psgId = m.PassageId == null ? null : passageMap.GetValueOrDefault(m.PassageId ?? 0);
                if (m.PassageId != null && psgId == 0)
                    psgId = m.ArtifactTypeId == null ? throw new Exception("Passage not found" + m.PassageId) : null;
                Mediafile copym = new()
                {
                    PassageId = psgId,
                    VersionNumber = Convert.ToBoolean(m.VersionNumber) ? m.VersionNumber : 1,
                    ArtifactTypeId = m.ArtifactTypeId == null ? null : artifacttypeMap?.GetValueOrDefault(m.ArtifactTypeId??0) ?? m.ArtifactTypeId,
                    EafUrl = m.EafUrl,
                    Duration = m.Duration,
                    ContentType = m.ContentType,
                    //AudioQuality = m.AudioQuality,
                    //TextQuality = m.TextQuality,
                    Transcription = m.Transcription,
                    PlanId = plan.Id,
                    OriginalFile = m.S3File ?? m.OriginalFile,
                    Filesize = m.Filesize,
                    Position = m.Position,
                    Segments = m.Segments,
                    Languagebcp47 = m.Languagebcp47,
                    Link = Convert.ToBoolean(m.Link),
                    PerformedBy = m.PerformedBy,
                    ReadyToShare = false,
                    ArtifactCategoryId = m.ArtifactCategoryId == null ? null : sameOrg ? ValidArtifactCategory(m.ArtifactCategoryId) : artifactcategoryMap?.GetValueOrDefault(m.ArtifactCategoryId??0),
                    ResourcePassageId = m.ResourcePassageId == null ? null : passageMap.GetValueOrDefault(m.ResourcePassageId??0) == 0 ? null : passageMap.GetValueOrDefault(m.ResourcePassageId??0),
                    RecordedbyUser = sameOrg ? m.RecordedbyUser : CurrentUser(),
                    OfflineId = "",
                    SourceMediaId = map.GetValueOrDefault(m.SourceMediaId??0)?.Id,
                    SourceSegments = m.SourceSegments,
                    SourceMediaOfflineId = "",
                    Transcriptionstate = m.Transcriptionstate,
                    Topic = m.Topic,
                };
                copym.S3File = mediaService.GetNewFileNameAsync(copym, suffix).Result;
                EntityEntry<Mediafile>? t =  dbContext.Mediafiles.Add(copym);
                map.Add(m.Id, t.Entity);
                //we have to save after every one because we may have a link to previous mediafiles here
                dbContext.SaveChanges();
                if (newProjId != null)
                {
                    SaveId(Tables.Mediafiles, m.Id, t.Entity.Id, newProjId??0);
                    dbContext.SaveChanges();
                    await CopyMediafile(m, t.Entity);
                }
            }
            return map;
        }
        private void CopyPassagestatechanges(IList<Passagestatechange> lst, Dictionary<int, int> passageMap)
        {
            foreach (Passagestatechange p in lst)
            {
                EntityEntry<Passagestatechange>? t = dbContext.Passagestatechanges.Add(
                    new Passagestatechange
                    {
                        PassageId= passageMap.GetValueOrDefault(p.PassageId),
                        State = p.State,
                        Comments = p.Comments,
                    });
            }
        }
        private Dictionary<int, int> CopyDiscussions(IList<Discussion> lst, bool sameOrg, Dictionary<int, 
            int>? acMap, Dictionary<int, int> mediafileMap, Dictionary<int, int>? orgwfMap)
        {
            Dictionary<int, Discussion> map = new();
            foreach (Discussion d in lst)
            {
                EntityEntry<Discussion>? t = dbContext.Discussions.Add(
                    new Discussion
                    {
                        ArtifactCategoryId = d.ArtifactCategoryId == null ? null : sameOrg ? d.ArtifactCategoryId : acMap?.GetValueOrDefault(d.ArtifactCategoryId??0),
                        MediafileId = d.MediafileId == null ? null : mediafileMap.GetValueOrDefault(d.MediafileId??0),
                        OrgWorkflowStepId = orgwfMap == null ? d.OrgWorkflowStepId : orgwfMap.GetValueOrDefault(d.OrgWorkflowStepId),
                        GroupId = sameOrg ? d.GroupId : null,
                        Resolved = d.Resolved,
                        Segments = d.Segments,
                        Subject = d.Subject,
                        UserId = d.User?.Id,
                        DateCreated = d.DateCreated,
                        DateUpdated = DateTime.UtcNow,
                    }
                );
                map.Add(d.Id, t.Entity);
            }
            dbContext.SaveChanges();
            Dictionary<int, int> result = new();
            foreach (KeyValuePair<int, Discussion> kvp in map)
                result.Add(kvp.Key, kvp.Value.Id);
            return result;
        }
        private void CopyComments(IList<Comment> lst, Dictionary<int,
            int> discussionMap, Dictionary<int, int> mediafileMap)
        {
            foreach (Comment c in lst)
            {
                int? mId = c.MediafileId == null ? null : mediafileMap.GetValueOrDefault(c.MediafileId??0);
                if (mId != 0)
                {
                    _ = dbContext.Comments.Add(
                        new Comment
                        {
                            OfflineId = "",
                            OfflineMediafileId = "",
                            OfflineDiscussionId = "",
                            DiscussionId = discussionMap.GetValueOrDefault(c.DiscussionId ?? 0),
                            CommentText = c.CommentText,
                            MediafileId = mId,
                            Visible = c.Visible,
                        }
                    );
                }
            }
        }

        private Dictionary<int, int>? GetArtifactCategoryMap (bool sameOrg, int newProjId) {
            if (sameOrg)
                return null;
            ArtifactCategoryMap ??= GetMap(Tables.ArtifactCategorys, newProjId);
            return ArtifactCategoryMap;
        }
        private Dictionary<int, int>? GetOrgworkflowstepMap(bool sameOrg, int newProjId)
        {
            if (sameOrg) return null;
            OrgworkflowstepMap ??= GetMap(Tables.OrgWorkflowSteps, newProjId);
            return OrgworkflowstepMap;
        }
        private Dictionary<int, int> GetSectionMap(int newProjId)
        {
            SectionMap ??= GetMap(Tables.Sections, newProjId);
            return SectionMap;
        }
        private Dictionary<int, int> GetPassageMap(int newProjId)
        {
            PassageMap ??= GetMap(Tables.Passages, newProjId);
            return PassageMap;
        }
        private Dictionary<int, int> GetMediafileMap(int newProjId)
        {
            MediafileMap ??= GetMap(Tables.Mediafiles, newProjId);
            return MediafileMap;
        }
        private Dictionary<int, int> GetDiscussionMap(int newProjId)
        {
            DiscussionMap ??= GetMap(Tables.Discussions, newProjId);
            return DiscussionMap;
        }
        private Dictionary<int, int> GetSectionResourceMap(int newProjId)
        {
            SectionResourceMap ??= GetMap(Tables.SectionResources, newProjId);
            return SectionResourceMap;
        }
        private void SaveId(string table, int oldId, int newId, int newProjId)
        {
            dbContext.Copyprojects.Add(new CopyProject()
            {
                Sourcetable = table,
                Newprojid = newProjId,
                Oldid = oldId,
                Newid = newId
            });
        }
        private void SaveMap(Dictionary<int, int> map, string table, int newProjId)
        {
            foreach (KeyValuePair<int, int> kvp in map)
            {
                SaveId(table, kvp.Key, kvp.Value, newProjId);
            }
            dbContext.SaveChanges();
        }
        private Dictionary<int, int> GetMap(string table, int newProjId)
        {
            Dictionary<int, int> map = new();
            IQueryable<CopyProject> cps = dbContext.Copyprojects.Where(c => c.Newprojid == newProjId && c.Sourcetable == table);
            foreach (CopyProject cp in cps)
            {
                map.Add(cp.Oldid, cp.Newid);
            }
            return map;
        }
        public void RemoveCopyProject(int projId)
        {
            foreach (CopyProject cp in dbContext.Copyprojects.Where(c => c.Newprojid == projId))
                dbContext.Remove(cp);
            dbContext.SaveChanges();
        }
        private int GetSingleId(string table, int projId)
        {
            CopyProject? cp = dbContext.Copyprojects.Where(c => c.Newprojid == projId && c.Sourcetable == table).FirstOrDefault();
            return cp?.Newid ?? 0;
        }
        private async Task<Fileresponse> ProcessImportCopyProjectAsync(
                Project sourceproject,
                bool sameOrg,
                int start,
                int? projId)
        {
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            User currentuser = CurrentUser() ?? new User();
            IQueryable<Organization> sourceOrg = dbContext.Organizations.Where(o => o.Id == sourceproject.OrganizationId);
            Organization org = sourceOrg.FirstOrDefault() ?? new Organization();
            IQueryable<Plan> sourceplans = dbContext.Plans.Where(p=>p.ProjectId == sourceproject.Id && !p.Archived);
            Project project = new();
            Plan plan = new();
            int newProjId = projId??0;
              
            if (start == 0)
            {
                int oldOrg = org.Id;
                if (!sameOrg)
                    org = await CreateNewOrg(org, false, currentuser);
                project = CreateNewProject(sourceproject, false, org.Id, currentuser);
                plan = await CreateNewPlan(sourceplans.First(), project, currentuser);
                newProjId = project.Id;
                SaveId(Tables.Organizations, oldOrg, org.Id, newProjId);
                SaveId(Tables.Plans, sourceplans.First().Id, plan.Id, newProjId);
                await dbContext.SaveChangesAsync();
                start++;
            } else
            {
                if (!sameOrg)
                {
                    Organization? tmporg = dbContext.Organizations.Where(o => o.Id == GetSingleId("organizations", newProjId)).FirstOrDefault();
                    if (tmporg == null)
                        return ErrorResponse("Can't find new organization",  sourceproject.Name);
                    org = tmporg;
                }
                Project? tmpProj = dbContext.Projects.Where(p => p.Id == newProjId).FirstOrDefault();
                if (tmpProj == null)
                    return ErrorResponse("Can't find new project", sourceproject.Name);
                project = tmpProj;
                Plan? tmpPlan = dbContext.Plans.Where(p => p.Id == GetSingleId(Tables.Plans,newProjId)).FirstOrDefault();
                if (tmpPlan == null)
                    return ErrorResponse("Can't find new plan", sourceproject.Name);
                plan = tmpPlan;
            }
            HttpContext?.SetFP("copy project");
            try
            {
                IQueryable<Section> sourcesections = sourceplans.Join(dbContext.Sections, p => p.Id, s => s.PlanId, (p, s) => s)
                                                    .Where(x => !x.Archived);
                IQueryable<Passage> sourcepassages = sourcesections.Join(dbContext.Passages, s => s.Id, p=> p.SectionId, (s, p) => p).Where(x => !x.Archived);
                IQueryable<Sectionresource> sectionresources = SectionResources(sourcesections);
                IEnumerable<Mediafile> sourcemediafiles = PlanSourceMedia(sectionresources);
                IQueryable<Mediafile>? myMedia = PlanMedia(sourceplans, OrgIPs(sourceOrg)).OrderBy(m => m.Id);
                
                int ix = start;
                string status = "";
                do
                {
                    string name = TableOrder.Keys.ElementAt(ix);
                    status = name;
                    switch (name)
                    {
                        case Tables.ArtifactCategorys:
                            if (!sameOrg)
                            {
                                ArtifactCategoryMap = CopyArtifactCategorys(dbContext.Artifactcategorys.Where(ac => ac.OrganizationId == null || ac.OrganizationId == sourceproject.OrganizationId).ToList(), org.Id);
                                SaveMap(ArtifactCategoryMap, name, newProjId);
                            }
                            ix++;
                            break;

                        case Tables.IntellectualPropertys:
                            if (!sameOrg)
                            {
                                //copy but change the organization to current org
                                CopyIP(OrgIPs(sourceOrg).ToList(), org.Id, GetMediafileMap(newProjId));
                            }
                            ix++;
                            break;

                        case Tables.OrgWorkflowSteps:
                            if (!sameOrg)
                            {
                                OrgworkflowstepMap = CopyOrgworkflowsteps(dbContext.Orgworkflowsteps.Where(s => s.OrganizationId == sourceproject.OrganizationId).ToList(), org.Id);
                                SaveMap(OrgworkflowstepMap, name, newProjId);
                            }
                            ix++;
                            break;

                        case Tables.OrgKeyTermTargets:
                            if (!sameOrg)
                            {
                                CopyOrgkeytermtargets(dbContext.Orgkeytermtargets.Where(s => s.OrganizationId == sourceproject.OrganizationId).ToList(), org.Id, GetMediafileMap(newProjId));
                            }
                            ix++;
                            break;

                        case Tables.Sections:
                            SectionMap = CopySections(sourcesections.ToList(), sameOrg, plan.Id, currentuser);
                            SaveMap(SectionMap, name, newProjId);
                            ix++;
                        break;

                        case Tables.Passages:
                            //save these for sectionresources next
                            PassageMap = CopyPassages(sourcepassages.ToList(), GetSectionMap(newProjId), GetOrgworkflowstepMap(sameOrg, newProjId));
                            SaveMap(PassageMap, name, newProjId);
                            ix++;
                            break;

                        case Tables.SectionResources:
                            SectionResourceMap = CopySectionResources(sectionresources.ToList(), org.Id,  project.Id, GetSectionMap(newProjId), GetMediafileMap(newProjId), GetOrgworkflowstepMap(sameOrg, newProjId), GetPassageMap(newProjId));
                            SaveMap(SectionResourceMap, name, newProjId);
                            ix++;
                            break;

                        case Tables.SectionResourceUsers:
                            if (sameOrg)
                            {
                                CopySectionResourceUsers(sectionresources
                                    .Join(
                                        dbContext.Sectionresourceusers,
                                        r => r.Id,
                                        u => u.SectionResourceId,
                                        (r, u) => u
                                    )
                                    .Where(x => !x.Archived)
                                    .ToList(), GetSectionResourceMap(newProjId));
                            }
                            ix++;
                            break;

                        case Tables.Mediafiles:
                            //Get any we did on a previous run
                            Dictionary<int, int>? prevmap = GetMediafileMap(newProjId);
                            Dictionary<int, Mediafile> map = await CopyMediafilesAsync(myMedia.ToList(),  sameOrg, plan, GetPassageMap(newProjId), null, GetArtifactCategoryMap(sameOrg, newProjId), prevmap.Count, newProjId, dtBail);
                            int total = myMedia.Count();
                            if (prevmap.Count + map.Count == total)
                            {
                                ix++;
                                MediafileMap = null;
                                //reset MediafileMap with all
                                GetMediafileMap(newProjId);
                            }
                            status = string.Format("{0} {1}/{2}", status, (prevmap.Count + map.Count), total);
                            break;

                        case Tables.PassageStateChanges:
                            CopyPassagestatechanges(sourcepassages.Join(dbContext.Passagestatechanges,
                                                                        p => p.Id, psc => psc.PassageId,
                                                                        (p, psc) => psc
                                                                    ).ToList(), GetPassageMap(newProjId));
                            ix++;
                            break;

                        case Tables.Discussions:
                            DiscussionMap = CopyDiscussions(PlanDiscussions(myMedia).ToList(), sameOrg, GetArtifactCategoryMap(sameOrg, newProjId), GetMediafileMap(newProjId), GetOrgworkflowstepMap(sameOrg, newProjId));
                            SaveMap(DiscussionMap, name, newProjId);
                            ix++;
                            break;

                        case Tables.Comments:
                            CopyComments(dbContext.Comments
                            .Join(PlanDiscussions(myMedia), c => c.DiscussionId, d => d.Id, (c, d) => c)
                            .Where(x => !x.Archived)
                            .ToList(), GetDiscussionMap(newProjId), GetMediafileMap(newProjId));
                            ix++;
                            break;

                        default:
                            ix++;
                            break;
                    }
                } while (DateTime.Now < dtBail && ix < TableOrder.Count);
                _ = dbContext.SaveChanges();
                bool complete = (ix == TableOrder.Count);
                return new Fileresponse()
                {
                    Id = complete ? -1 : ix,
                    Message = string.Format("{0} {1} {2} {3}", org.Id, org.Name, project.Name,  complete ? "" : status),
                    FileURL = newProjId.ToString(),
                    Status = HttpStatusCode.OK,
                    ContentType = "application/ptf",
                };
                
            }
            catch (Exception ex)
            {
                return ErrorResponse(
                    ex.Message
                        + (
                            ex.InnerException != null && ex.InnerException.Message != ""
                                ? "=>" + ex.InnerException.Message
                                : ""
                        ),
                    newProjId.ToString()
                );
            }
            
        }
        private async Task<Fileresponse> ProcessImportCopyFileAsync(
                ZipArchive archive,
                bool neworg,
                string sFile)
        {
            User currentuser = CurrentUser() ?? new User();
            Organization org = new();
            Project project = new();
            Plan plan = new();

            //These were originally in the wrong order in the ptf file
            //but need to wait until after mediafiles
            IList<ResourceObject>? ipLst = null;
            IList<ResourceObject>? srLst = null;
            IList<ResourceObject>? srUserLst = null;

            IJsonApiOptions options = new JsonApiOptions();
            DateTime? sourceDate = CheckSILTranscriber(archive);
            if (sourceDate==null)
                return ErrorResponse("SILTranscriber not present", sFile);

            HttpContext?.SetFP("copy");
            //check project
            Project? sourceproject = ReadFileProject(archive);
#pragma warning disable CS8604 // Possible null reference argument.
            bool sameOrg = !neworg && sourceproject != null;
            if (sameOrg)
            {
                int orgid = sourceproject?.OrganizationId??0;
                org = dbContext.Organizations.FirstOrDefault(o => o.Id == orgid) ?? new Organization();
                if (org.Id == 0)
                    sameOrg = false;
            } 
            try
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!entry.FullName.StartsWith("data"))
                        continue;
                    string name = Path.GetFileNameWithoutExtension(entry.Name[2..]);
                    Logger.LogInformation("{n} {cl} {l}", entry.FullName, entry.CompressedLength, entry.Length);
                    string? json = new StreamReader(entry.Open()).ReadToEnd();
                    Document? doc = JsonSerializer.Deserialize<Document>(
                        json,
                        options.SerializerReadOptions
                    );
                    IList<ResourceObject>? lst = doc?.Data.ManyValue;
                    if (doc == null || lst == null)
                        continue;
                    Logger.LogInformation("name: {0}", name);

                    switch (name)
                    {
                        case Tables.Organizations:
                            if (sameOrg)
                                continue;
                            Organization fileorg = ResourceObjectToResource(lst.First(), new Organization());
                            bool samename = !neworg;
                            if (!neworg) {
                                Organization? existing = dbContext.Organizations.FirstOrDefault(o => o.Name == fileorg.Name && o.OwnerId == currentuser.Id && !o.Archived);
                                if (existing != null)
                                {
                                    org = existing;
                                }
                                else
                                    neworg = true;
                            }
                            if (org.Id == 0)
                                org = await CreateNewOrg(fileorg, samename, currentuser);
                            break;

                        case Tables.ArtifactCategorys:
                            if (sameOrg)
                                continue;
                            List<Artifactcategory> ac = new();
                            foreach (ResourceObject ro in lst)
                                ac.Add(ResourceObjectToResource(ro, new Artifactcategory()));
                            ArtifactCategoryMap = CopyArtifactCategorys(ac.Where(s => s.OrganizationId == sourceproject?.OrganizationId || s.OrganizationId is null).ToList(), org.Id);
                            break;

                        case Tables.ArtifactTypes:
                            if (sameOrg)
                                continue;
                            ArtifactTypesMap = MapArtifactTypes(lst);
                            break;

                        case Tables.IntellectualPropertys:
                            //copy but change the organization to current org
                            if (!neworg) //we don't need a map so skip if we aren't a new org
                                continue;
                            ipLst = lst;
                            continue;

                        case Tables.OrgWorkflowSteps:
                            if (sameOrg)
                                continue;
                               
                            List<Orgworkflowstep> owlst = new();
                            foreach (ResourceObject ro in lst)
                                owlst.Add(ResourceObjectToResource(ro, new Orgworkflowstep()));
                            OrgworkflowstepMap = CopyOrgworkflowsteps(owlst.Where(s => s.OrganizationId == sourceproject?.OrganizationId).ToList(), org.Id);
                            break;

                        case Tables.Projects:
                            project = CreateNewProject(ResourceObjectToResource(lst.First(), new Project()), !sameOrg, org.Id, currentuser);
                            break;

                        case Tables.Plans:
                            plan = await CreateNewPlan(ResourceObjectToResource(lst.First(), new Plan()), project, currentuser);
                            break;

                        case Tables.Sections:
                            List<Section> slst = new();
                            foreach (ResourceObject ro in lst)
                                slst.Add(ResourceObjectToResource(ro, new Section()));
                            SectionMap = CopySections(slst, sameOrg, plan.Id, currentuser);
                            break;

                        case Tables.Passages:
                            List<Passage> plst = new();
                            foreach (ResourceObject ro in lst)
                                plst.Add(ResourceObjectToResource(ro, new Passage()));
                            PassageMap = CopyPassages(plst, SectionMap, OrgworkflowstepMap);
                            break;

                        case Tables.SectionResources:
                            srLst = lst;
                            break;

                        case Tables.SectionResourceUsers:
                            if (!sameOrg)
                                break;
                            srUserLst = lst;
                            break;

                        case Tables.Mediafiles:
                            List<Mediafile> mflst = new();
                            foreach (ResourceObject ro in lst)
                                mflst.Add(ResourceObjectToResource(ro, new Mediafile()));
                            Dictionary<int, Mediafile> map = await CopyMediafilesAsync(mflst, sameOrg, plan, PassageMap, ArtifactTypesMap, ArtifactCategoryMap, 0, null, null);
                            Dictionary<int, int> result = new();
                            foreach (KeyValuePair<int, Mediafile> kvp in map)
                                result.Add(kvp.Key, kvp.Value.Id);
                            MediafileMap = result;
                            foreach (Mediafile copym in map.Values)
                                await CopyMediaFile(copym, archive);
                            break;

                        case Tables.PassageStateChanges:
                            List<Passagestatechange> psclist = new();
                            foreach(ResourceObject ro in lst)
                                psclist.Add(ResourceObjectToResource(ro, new Passagestatechange()));
                            CopyPassagestatechanges(psclist, PassageMap);
                            break;

                         case Tables.Discussions:
                            List<Discussion> dlst = new();
                            foreach (ResourceObject ro in lst)
                                dlst.Add(ResourceObjectToResource(ro, new Discussion()));
                            DiscussionMap = CopyDiscussions(dlst,sameOrg, ArtifactCategoryMap, MediafileMap, OrgworkflowstepMap);
                            break;

                        case Tables.Comments:
                            List<Comment> clst = new();
                            foreach (ResourceObject ro in lst)
                                clst.Add(ResourceObjectToResource(ro, new Comment()));
                            CopyComments(clst, DiscussionMap, MediafileMap);
                            break;
                    }
                }
                if (ipLst != null)
                {
                    List<Intellectualproperty> lst = new();
                    foreach (ResourceObject ro in ipLst)
                        lst.Add(ResourceObjectToResource(ro, new Intellectualproperty()));
                    CopyIP(lst, org.Id, MediafileMap);
                }
                if (srLst != null)
                {
                    List<Sectionresource> lst = new();
                    foreach (ResourceObject ro in srLst)
                        lst.Add(ResourceObjectToResource(ro, new Sectionresource()));
                    SectionResourceMap = CopySectionResources(lst, org.Id, project.Id, SectionMap, MediafileMap, OrgworkflowstepMap, PassageMap);
                    if (srUserLst != null)
                    {
                        List<Sectionresourceuser> ulst = new();
                        foreach (ResourceObject ro in srUserLst)
                            ulst.Add(ResourceObjectToResource(ro, new Sectionresourceuser()));
                        CopySectionResourceUsers(ulst, SectionResourceMap);
                    }
                }

                _ = dbContext.SaveChanges();

                return new Fileresponse()
                {
                    Message = org.Id.ToString() + org.Name + " " + project.Name,
                    FileURL = sFile,
                    Status = HttpStatusCode.OK,
                    ContentType = "application/ptf",
                };
            }
            catch (Exception ex)
            {
                return ErrorResponse(
                    ex.Message
                        + (
                            ex.InnerException != null && ex.InnerException.Message != ""
                                ? "=>" + ex.InnerException.Message
                                : ""
                        ),
                    sFile
                );
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }
    }
    #endregion Copy
}
