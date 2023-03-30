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

namespace SIL.Transcriber.Services
{
    public class OfflineDataService : IOfflineDataService
    {
        protected readonly AppDbContext dbContext;
        protected readonly MediafileService mediaService;
        protected CurrentUserRepository CurrentUserRepository { get; }

        readonly private IS3Service _S3service;
        readonly private ISQSService _SQSservice;
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
            {"users",'A'},
            {"activitystates",'B'},
            {"integrations",'B'},
            {"organizations",'B'},
            {"plantypes",'B'},
            {"projecttypes",'B'},
            {"roles",'B'},
            {"workflowsteps",'B'},
            {"artifactcategorys",'C'},
            {"artifacttypes",'C'},
            {"groups",'C'},
            {"organizationmemberships",'C'},
            {"orgkeyterms",'C'},
            {"orgworkflowsteps",'C'},
            {"groupmemberships",'D'},
            {"projects",'D'},
            {"invitations",'D'},
            {"plans",'E'},
            {"projectintegrations",'E'},
            {"sections",'F'},
            {"passages",'G'},
            {"mediafiles",'H'},
            {"orgkeytermreferences",'H'},
            {"passagestatechanges",'H'},
            {"orgkeytermtargets",'I'},
            {"sectionresources",'I'},
            {"discussions",'I'},
            {"intellectualpropertys",'I'},
            {"sharedresources", 'I' },
            {"comments",'J'},
            {"sectionresourceusers",'J'},
            {"sharedresourcereferences", 'J' }
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
            _S3service = service;
            _SQSservice = sqsService;
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
            IList<TResource> list,
            char sort
        ) where TResource : class, IIdentifiable
        {
            ZipArchiveEntry entry = zipArchive.CreateEntry(
                "data/" + sort + "_" + table + ".json",
                CompressionLevel.Fastest
            );
            WriteEntry(entry, ToJson(list));
        }

        private static void AddEafEntry(ZipArchive zipArchive, string name, string eafxml)
        {
            if (!string.IsNullOrEmpty(eafxml))
            {
                ZipArchiveEntry entry = zipArchive.CreateEntry(
                    "media/" + Path.ChangeExtension(name, ".eaf"),
                    CompressionLevel.Optimal
                );
                WriteEntry(entry, eafxml);
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
                using (Stream zipEntryStream = entry.Open())
                {
                    //Copy the attachment stream to the zip entry stream
                    fileStream.CopyTo(zipEntryStream);
                }
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
            byte[]? imageData = null;
            try
            {
                using HttpClient client = new ();
                using HttpResponseMessage response = await client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead
                );
                using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();
                string fileToWriteTo = Path.GetTempFileName();
                using Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create);
                await streamToReadFrom.CopyToAsync(streamToWriteTo);
            }
            catch
            {
                return null;
            }
            return imageData != null ? new MemoryStream(imageData) : null;
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
        private static void AddFont(ZipArchive zipArchive, HttpClient client, string cssfile)
        {
            string bucket = "https://s3.amazonaws.com/fonts.siltranscriber.org/";
            try
            {
                /* read the css file */
                string url = bucket + cssfile;
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
                    string fontfile = url[(url.LastIndexOf("/") + 1)..];
                    url = bucket + fontfile;
                    _ = AddStreamEntry(zipArchive, url, "fonts/", fontfile);
                    css = css [..(start + 1)] + fontfile + css [end..];
                }
                ZipArchiveEntry entry = zipArchive.CreateEntry(
                    "fonts/" + cssfile,
                    CompressionLevel.Fastest
                );
                WriteEntry(entry, css);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Font file not found {0}", cssfile);
                Console.WriteLine(ex);
            }
        }

        private static void AddFonts(ZipArchive zipArchive, IEnumerable<string> fonts)
        {
            using HttpClient client = new ();
            foreach (string f in fonts)
            {
                string cssfile = f.Split(',')[0].Replace(" ", "") + ".css";
                AddFont(zipArchive, client, cssfile);
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
                new string(Path.GetInvalidFileNameChars()) + "'"
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

            string sanitisedName = System.Text.RegularExpressions.Regex.Replace(
                filename,
                invalidReStr,
                "_"
            );
            while (sanitisedName.IndexOf("__") > -1)
                sanitisedName = sanitisedName.Replace("__", "_");

            foreach (string reservedWord in reservedWords)
            {
                string reservedWordPattern = string.Format("^{0}(\\.|$)", reservedWord);
                sanitisedName = System.Text.RegularExpressions.Regex.Replace(
                    sanitisedName,
                    reservedWordPattern,
                    "_reservedWord_$1",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }

            return sanitisedName;
        }

        private bool CheckAdd<TResource>(
            int check,
            DateTime dtBail,
            ref int completed,
            ZipArchive zipArchive,
            string table,
            IList<TResource> list,
            char sort
        ) where TResource : class, IIdentifiable
        {
            Logger.LogInformation("{check} : {dt} {dtBail}", check, DateTime.Now, dtBail);
            if (DateTime.Now > dtBail)
                return false;
            if (completed <= check)
            {
                AddJsonEntry(zipArchive, table, list, sort);
                completed++;
            }
            return true;
        }

        private Fileresponse CheckProgress(string fileName, int lastAdd)
        {
            int startNext;
            string err = "";
            bool recent = false;
            try
            {
                Stream ms = OpenFile(fileName + ".sss", out recent);
                if (recent)
                {
                    StreamReader reader = new(ms);
                    string data = reader.ReadToEnd();
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
                Logger.LogInformation("{sf} status file not available", fileName + ".sss");
                startNext = lastAdd + 1;
            }
            if (startNext < 0)
            {
                try
                {
                    S3Response resp = _S3service.RemoveFile(fileName + ".sss", ExportFolder).Result;
                    resp = _S3service.RemoveFile(fileName + ".tmp", ExportFolder).Result;
                }
                catch { }
                ;
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
                        ? _S3service.SignedUrlForGet(fileName, ExportFolder, contentType).Message
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

        private Stream OpenFile(string fileName, out bool recent)
        {
            S3Response s3response = _S3service.ReadObjectDataAsync(fileName, ExportFolder).Result;
            _ = bool.TryParse(s3response.Message, out recent);
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
                    S3Response resp = _S3service.RemoveFile(fileName + ".sss", ExportFolder).Result;
                }
                catch { }
                ;
            }
            else
            {
                ms = OpenFile(fileName + ext, out bool recent);
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
            s3response = _S3service
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

            dynamic? root = Newtonsoft.Json.JsonConvert.DeserializeObject(metastr);
            if (root == null)
                throw new Exception("Bad Meta" + metastr);
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
                    passage.StartChapter.ToString().PadLeft(3, '0'),
                    passage.StartVerse.ToString().PadLeft(3, '0'),
                    passage.EndVerse.ToString().PadLeft(3, '0'),
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
            AddJsonEntry(zipArchive, "attachedmediafiles", mediafiles.Concat(ipMedia).ToList<Mediafile>(), 'Z');
            return mediafiles;
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
                                passage.StartChapter.ToString().PadLeft(3, '0'),
                                passage.StartVerse.ToString().PadLeft(3, '0'),
                                passage.StartVerse == passage.EndVerse ? "" :
                                '-' + passage.EndVerse.ToString().PadLeft(3, '0')
                            ) : string.Format(
                                "{0}_{1}-{2}_{3}",
                                passage.StartChapter.ToString().PadLeft(3, '0'),
                                passage.StartVerse.ToString().PadLeft(3, '0'),
                                passage.EndChapter.ToString().PadLeft(3, '0'),
                                passage.EndVerse.ToString().PadLeft(3, '0'))
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
            AddJsonEntry(zipArchive, "attachedmediafiles", mediafiles, 'Z');
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
                    AddJsonEntry(zipArchive, "mediafiles", mediafiles, 'H');
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
            string id= _SQSservice.SendExportMessage(project.Id, ExportFolder, fileName + ext, 0);
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
            string id= _SQSservice.SendExportMessage(project.Id, ExportFolder, fileName + ext, 0);
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
        private IQueryable<Mediafile> PlanMedia(IQueryable<Plan> plans, IQueryable<Intellectualproperty> ip)
        {
            IQueryable<Mediafile> xmediafiles = plans
                        .Join(dbContext.MediafilesData, p => p.Id, m => m.PlanId, (p, m) => m)
                        .Where(x => !x.Archived);
            IQueryable<Mediafile>? ipmedia = ip.Join(dbContext.MediafilesData, ip => ip.ReleaseMediafileId, m=> m.Id, (ip, m) => m).Where(x => !x.Archived);
            IQueryable<Mediafile>? myMedia = xmediafiles.Concat(ipmedia).Distinct();
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
                    mf.AudioUrl = _S3service
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
        public Fileresponse ExportProjectPTF(int projectid, int start)
        {
            const int LAST_ADD = 19;
            const string ext = ".ptf";
            int startNext = start;
            //give myself 15 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(15);

            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == projectid);
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
                return CheckProgress(fileName + ext, LAST_ADD);

            Stream ms = GetMemoryStream(start, fileName, ext);
            if (start >= 0)
            {
                using ZipArchive zipArchive = new(ms, ZipArchiveMode.Update, true);
                IQueryable<Organization> orgs = dbContext.Organizations.Where(
                        o => o.Id == project.OrganizationId
                    );
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
                        "activitystates",
                        dbContext.Activitystates.ToList(),
                        TableOrder.GetValueOrDefault("activitystates")
                    );
                    AddJsonEntry(zipArchive, "integrations", dbContext.Integrations.ToList(), TableOrder.GetValueOrDefault("integrations"));
                    AddJsonEntry(zipArchive, "plantypes", dbContext.Plantypes.ToList(), TableOrder.GetValueOrDefault("plantypes"));
                    AddJsonEntry(zipArchive, "projecttypes", dbContext.Projecttypes.ToList(), TableOrder.GetValueOrDefault("projecttypes"));
                    AddJsonEntry(zipArchive, "roles", dbContext.Roles.ToList(), TableOrder.GetValueOrDefault("roles"));
                    AddJsonEntry(
                        zipArchive,
                        "workflowsteps",
                        dbContext.Workflowsteps.ToList(),
                        TableOrder.GetValueOrDefault("workflowsteps")
                    );
                    //org
                    List<Organization> orgList = orgs.ToList();

                    AddOrgLogos(zipArchive, orgList);
                    AddJsonEntry(zipArchive, "organizations", orgList, TableOrder.GetValueOrDefault("organizations"));

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
                        "intellectualpropertys",
                        ip.ToList(),
                        TableOrder.GetValueOrDefault("intellectualpropertys")
                    );
                    AddJsonEntry(
                        zipArchive,
                        "groups",
                        groups.Where(g => !g.Archived).ToList(),
                        TableOrder.GetValueOrDefault("groups")
                    );
                    //groupmemberships
                    AddJsonEntry(zipArchive, "groupmemberships", gms, TableOrder.GetValueOrDefault("groupmemberships"));
                    AddJsonEntry(zipArchive, "users", userList, TableOrder.GetValueOrDefault("users"));

                    //organizationmemberships
                    IEnumerable<Organizationmembership> orgmems = users
                        .Join(
                            dbContext.Organizationmemberships,
                            u => u.Id,
                            om => om.UserId,
                            (u, om) => om
                        )
                        .Where(om => om.OrganizationId == project.OrganizationId && !om.Archived);
                    AddJsonEntry(zipArchive, "organizationmemberships", orgmems.ToList(), TableOrder.GetValueOrDefault("organizationmemberships"));

                    //projects
                    AddJsonEntry(zipArchive, "projects", projects.ToList(), TableOrder.GetValueOrDefault("projects"));
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
                            "projectintegrations",
                            projects
                                .Join(
                                    dbContext.Projectintegrations,
                                    p => p.Id,
                                    pi => pi.ProjectId,
                                    (p, pi) => pi
                                )
                                .Where(x => !x.Archived)
                                .ToList(),
                            TableOrder.GetValueOrDefault("projectintegrations")
                        )
                    )
                        break;
                    //plans
                    IQueryable<Plan> plans = projects
                        .Join(dbContext.Plans, p => p.Id, pl => pl.ProjectId, (p, pl) => pl)
                        .Where(x => !x.Archived);
                    if (
                        !CheckAdd(
                            2,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "plans",
                            plans.ToList(),
                            TableOrder.GetValueOrDefault("plans")
                        )
                    )
                        break;
                    //sections
                    IQueryable<Section> sections = plans
                        .Join(dbContext.Sections, p => p.Id, s => s.PlanId, (p, s) => s)
                        .Where(x => !x.Archived);
                    if (
                        !CheckAdd(
                            3,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "sections",
                            sections.ToList(),
                            TableOrder.GetValueOrDefault("sections")
                        )
                    )
                        break;
                    //passages
                    IQueryable<Passage> passages = sections
                        .Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p)
                        .Where(x => !x.Archived);
                    if (
                        !CheckAdd(
                            4,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "passages",
                            passages.ToList(),
                            TableOrder.GetValueOrDefault("passages")
                        )
                    )
                        break;
                    //passagestatechange
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
                            "passagestatechanges",
                            passagestatechanges.ToList(),
                            TableOrder.GetValueOrDefault("passagestatechanges")
                        )
                    )
                        break;
                    //mediafiles
                    //I need my mediafiles plus any shared resource mediafiles
                    IQueryable<Sectionresource> sectionresources = SectionResources(sections);

                    IEnumerable<Mediafile> sourcemediafiles = PlanSourceMedia(sectionresources);
                    IQueryable<Mediafile>? myMedia = PlanMedia(plans, ip);


                    if (
                        !CheckAdd(
                            6,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "mediafiles",
                            myMedia.OrderBy(m => m.Id).ToList(),
                            TableOrder.GetValueOrDefault("mediafiles")
                        )
                    )
                        break;

                    if (
                        !CheckAdd(
                            7,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "artifactcategorys",
                            dbContext.Artifactcategorys
                                .Where(a =>
                                        (
                                            a.OrganizationId == null
                                            || a.OrganizationId == project.OrganizationId
                                        ) && !a.Archived
                                )
                                .ToList(),
                            TableOrder.GetValueOrDefault("artifactcategorys")
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            8,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "artifacttypes",
                            dbContext.Artifacttypes
                                .Where(a =>
                                        (
                                            a.OrganizationId == null
                                            || a.OrganizationId == project.OrganizationId
                                        ) && !a.Archived
                                )
                                .ToList(),
                            TableOrder.GetValueOrDefault("artifacttypes")
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            9,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "orgworkflowsteps",
                            dbContext.OrgworkflowstepsData
                                .Where(
                                    a => (a.OrganizationId == project.OrganizationId) && !a.Archived
                                )
                                .ToList(),
                            TableOrder.GetValueOrDefault("orgworkflowsteps")
                        )
                    )
                        break;
                    //only limit vernacular to those with passageids
                    IQueryable<Mediafile> attachedmediafiles = AttachedMedia(myMedia);

                    IQueryable<Discussion> discussions = PlanDiscussions(myMedia);
                    if (
                        !CheckAdd(
                            10,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "discussions",
                            discussions.ToList(),
                            TableOrder.GetValueOrDefault("discussions")
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            11,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "comments",
                            dbContext.Comments
                                .Join(discussions, c => c.DiscussionId, d => d.Id, (c, d) => c)
                                .Where(x => !x.Archived)
                                .ToList(),
                            TableOrder.GetValueOrDefault("comments")
                        )
                    )
                        break;

                    if (
                        !CheckAdd(
                            12,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "sectionresources",
                            sectionresources.ToList(),
                            TableOrder.GetValueOrDefault("sectionresources")
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            13,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "sectionresourceusers",
                            sectionresources
                                .Join(
                                    dbContext.Sectionresourceusers,
                                    r => r.Id,
                                    u => u.SectionResourceId,
                                    (r, u) => u
                                )
                                .Where(x => !x.Archived)
                                .ToList(),
                            TableOrder.GetValueOrDefault("sectionresourceusers")
                        )
                    )
                        break;

                    IQueryable<Orgkeyterm>? orgkeyterms = dbContext.OrgKeytermsData
                                    .Where(
                                        a => (a.OrganizationId == project.OrganizationId) && !a.Archived
                                    );
                    if (
                        !CheckAdd(
                            14,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "orgkeyterms",
                            orgkeyterms.ToList(),
                            TableOrder.GetValueOrDefault("orgkeyterms")
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            15,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "orgkeytermreferences",
                            dbContext.OrgKeytermReferencesData
                                .Join(orgkeyterms, r => r.OrgkeytermId, k => k.Id, (r, k) => r)
                                .ToList(),
                            TableOrder.GetValueOrDefault("orgkeytermreferences")
                        )
)
                        break;
                    if (
                        !CheckAdd(
                            16,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "orgkeytermtargets",
                            dbContext.OrgKeytermTargetsData.Where(
                                    a => (a.OrganizationId == project.OrganizationId) && !a.Archived
                                )
                                .ToList(),
                            TableOrder.GetValueOrDefault("orgkeytermtargets")
                        )
                    )
                        break;
                    IQueryable<Sharedresource>? sharedresources = dbContext.SharedresourcesData
                                    .Where(a => !a.Archived);
                    if (
                        !CheckAdd(
                            17,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "sharedresources",
                            sharedresources.ToList(),
                            TableOrder.GetValueOrDefault("sharedresources")
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            18,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "sharedresourcereferences",
                            dbContext.SharedresourcereferencesData
                                .Join(sharedresources, r => r.SharedResourceId, k => k.Id, (r, k) => r)
                                .ToList(),
                            TableOrder.GetValueOrDefault("sharedresourcereferences")
                        )
                    )
                        break;

                    //Now I need the media list of just those files to download...
                    //pick just the highest version media per passage (vernacular only) for eaf (TODO: what about bt?!)
                    IQueryable<Mediafile> vernmediafiles =
                        from m in attachedmediafiles
                        where m.ArtifactTypeId == null
                        group m by m.PassageId into grp
                        select grp.OrderByDescending(m => m.VersionNumber).FirstOrDefault();

                    if (!AddMediaEaf(19, dtBail, ref startNext, zipArchive, vernmediafiles.ToList(), null))
                        break;
                    List <Mediafile> mediaList  = attachedmediafiles.ToList().Concat(sourcemediafiles.ToList()).ToList();
                    AddAttachedMedia(zipArchive, mediaList, null);
                } while (false);
            }
            Fileresponse response = WriteMemoryStream(ms, fileName, startNext, ext);
            if (startNext == LAST_ADD + 1)
            {   //add the mediafiles
                string id= _SQSservice.SendExportMessage(project.Id, ExportFolder, fileName + ext, 0);
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
                Path.GetFileNameWithoutExtension(sFile),
                DateTime.Now.Ticks,
                extension
            );
            //get a signedurl for it now
            return new Fileresponse()
            {
                Message = fileName,
                FileURL = _S3service.SignedUrlForPut(fileName, ImportFolder, ContentType).Message,
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
            return ChangesReport("user", Serialize(online), Serialize(imported));
        }

        private string SectionChangesReport(Section online, Section imported)
        {
            return (online.EditorId != imported.EditorId && online.EditorId != null)
                || (online.TranscriberId != imported.TranscriberId && online.TranscriberId != null)
                || online.State != imported.State
                ? ChangesReport("section", Serialize(online), Serialize(imported))
                : "";
        }

        private string PassageChangesReport(Passage online, Passage imported)
        {
            return online.StepComplete != imported.StepComplete
                ? ChangesReport("passage", Serialize(online), Serialize(imported))
                : "";
        }

        private string MediafileChangesReport(Mediafile online, Mediafile imported)
        {
            if (online.Transcription != imported.Transcription && online.Transcription != null)
            {
                Mediafile copy = (Mediafile)online.ShallowCopy();
                copy.AudioUrl = "";
                return ChangesReport("mediafile", Serialize(copy), Serialize(imported));
            }
            return "";
        }

        private string DiscussionChangesReport(Discussion online, Discussion imported)
        {
            return online.ArtifactCategoryId != imported.ArtifactCategoryId
                || online.GroupId != imported.GroupId
                || online.Resolved != imported.Resolved
                ? ChangesReport("discussion", Serialize(online), Serialize(imported))
                : "";
        }

        private string CommentChangesReport(Comment online, Comment imported)
        {
            return online.CommentText != imported.CommentText
                || online.MediafileId != imported.MediafileId
                ? ChangesReport("comment", Serialize(online), Serialize(imported))
                : "";
        }

        private string GrpMemChangesReport(Groupmembership online, Groupmembership imported)
        {
            return online.FontSize != imported.FontSize
                ? ChangesReport("groupmembership", Serialize(online), Serialize(imported))
                : "";
        }

        public async Task<Fileresponse> ImportFileAsync(string sFile)
        {
            S3Response response = await _S3service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return new Fileresponse()
                {
                    Message = "File not found",
                    FileURL = sFile,
                    Status = HttpStatusCode.NotFound,
                    ContentType = "application/itf",
                };
            ZipArchive archive = new(response.FileStream);
            List<string> report = new();
            List<string> errors = new();
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                ZipArchive zipentry = new(entry.Open());
                Fileresponse fr = await ProcessImportFileAsync(zipentry, 0, entry.Name);
                if (fr.Status == HttpStatusCode.OK)
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
                    Status = HttpStatusCode.OK,
                    ContentType = "application/itf",
                };
        }

        public async Task<Fileresponse> ImportFileAsync(int projectid, string sFile)
        {
            S3Response response = await _S3service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return new Fileresponse()
                {
                    Message = "File not found",
                    FileURL = sFile,
                    Status = HttpStatusCode.NotFound,
                    ContentType = "application/itf",
                };
            ZipArchive archive = new(response.FileStream);
            return await ProcessImportFileAsync(archive, projectid, sFile);
        }
        public async Task<Fileresponse> ImportCopyFileAsync(bool neworg, string sFile)
        {
            S3Response response = await _S3service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return new Fileresponse()
                {
                    Message = "File not found",
                    FileURL = sFile,
                    Status = HttpStatusCode.NotFound,
                    ContentType = "application/ptf",
                };
            ZipArchive archive = new(response.FileStream);
            return await ProcessImportCopyFileAsync(archive, neworg, sFile);
        }
        public async Task<Fileresponse> ImportCopyProjectAsync(bool neworg, int projectid, int start, int? newProjId)
        {
            Project? sourceproject = dbContext.Projects.FirstOrDefault(p => p.Id==projectid);
            return sourceproject == null
                ? ErrorResponse("Project not found", projectid.ToString())
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
            };
        }

        private async Task<bool> CopyMediafile(Mediafile source, Mediafile? target)
        {
            if (source.S3File != null && target?.S3File != null)
            {
                S3Response response = await _S3service.CopyFile(source.S3File, target.S3File, mediaService.DirectoryName(source), mediaService.DirectoryName(target));
                if (response.Status == HttpStatusCode.OK)
                {
                    target.AudioUrl = _S3service.SignedUrlForGet(
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
                S3Response response = await _S3service.UploadFileAsync(
                    ms,
                    true,
                    m.ContentType ?? "",
                    m.S3File ?? "",
                    mediaService.DirectoryName(m)
                );
                m.AudioUrl = _S3service
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

        private int CompareMediafilesByArtifactTypeVersionDesc(Mediafile a, Mediafile b)
        {
            if (a == null)
            {
                return b == null ? 0 : -1;
            }
            else
            { //a is not null
                if (b == null)
                    return 1;
                else
                { //neither a nor b is null
                    if (a.IsVernacular)
                    {
                        if (b.IsVernacular)
                            return a.VersionNumber ?? 0 - b.VersionNumber ?? 0; //both vernacular so use version number
                        else
                            return -1;
                    }
                    else
                    {
                        return b.IsVernacular ? 1 : 0;
                    }
                }
            }
        }

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
                existing.ArtifactCategoryId = importing.ArtifactCategoryId;
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
                existing.Transcription = importing.Transcription;
                if (importing.Transcriptionstate != null) //from old desktop
                    existing.Transcriptionstate = importing.Transcriptionstate;
                existing.LastModifiedBy = importing.LastModifiedBy;
                existing.LastModifiedByUser = importing.LastModifiedByUser;
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
        private Project? GetFileProject(ZipArchive archive)
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

            return fileproject == null ? null : dbContext.Projects.Find(int.Parse(fileproject.Id ?? "0"));
        }
        private async Task<Fileresponse> ProcessImportFileAsync(
            ZipArchive archive,
            int projectid,
            string sFile
        )
        {
            IJsonApiOptions options = new JsonApiOptions();
            List<string> report = new();
            List<string> deleted = new();
            try
            {
                ZipArchiveEntry? checkEntry = archive.GetEntry("SILTranscriberOffline");
                //var exportTime = new StreamReader(checkEntry.Open()).ReadToEnd();
            }
            catch
            {
                return ErrorResponse("SILTranscriberOffline not present", sFile);
            }
            DateTime? getsourceDate = CheckSILTranscriber(archive);
            if (getsourceDate == null)
                return ErrorResponse("SILTranscriber not present", sFile);
            //force it to a not nullable type
            DateTime sourceDate = getsourceDate ?? DateTime.Now;

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
                    if (project.Archived)
                        return ProjectDeletedResponse(project.Name, sFile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return ErrorResponse(
                    "Invalid ITF File - error finding project -" + ex.Message,
                    sFile
                );
            }

            try
            {
                if (project.Archived)
                {
                    report.Add(ProjectDeletedReport(project));
                }
                else
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (!entry.FullName.StartsWith("data"))
                            continue;
                        string name = Path.GetFileNameWithoutExtension(entry.Name[2..]);
                        string? json = new StreamReader(entry.Open()).ReadToEnd();
                        Document? doc = JsonSerializer.Deserialize<Document>(
                            json,
                            options.SerializerReadOptions
                        );
                        IList<ResourceObject>? lst = doc?.Data.ManyValue;
                        if (doc == null || lst == null)
                            continue;
                        switch (name)
                        {
                            case "users":
                                foreach (ResourceObject ro in lst)
                                {
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
                                break;

                            case "sections":
                                foreach (ResourceObject ro in lst)
                                {
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
                                ;
                                break;

                            case "passages":
                                int? currentuser = CurrentUser()?.Id;
                                foreach (ResourceObject ro in lst)
                                {
                                    Passage p = ResourceObjectToResource(ro, new Passage());
                                    Passage? passage = dbContext.Passages.Find(p.Id);
                                    if (
                                        passage != null
                                        && !passage.Archived
                                        && (
                                            passage.StepComplete != p.StepComplete
                                            || passage.State != p.State
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
                                ;
                                break;

                            case "discussions":
                                foreach (ResourceObject ro in lst)
                                {
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
                                                    ArtifactCategoryId = d.ArtifactCategoryId,
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
                                break;

                            case "comments":
                                foreach (ResourceObject ro in lst)
                                {
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
                                break;

                            case "mediafiles":
                                List<Mediafile> sorted = new();
                                foreach (ResourceObject ro in lst)
                                {
                                    sorted.Add(ResourceObjectToResource(ro, new Mediafile()));
                                }
                                sorted.Sort(CompareMediafilesByArtifactTypeVersionDesc);
                                foreach (Mediafile m in sorted)
                                {
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
                                        mediafile = dbContext.Mediafiles.FirstOrDefault(
                                            x => x.OfflineId == m.OfflineId
                                        );
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
                                                        m.VersionNumber =
                                                            mediafile.VersionNumber + 1;
                                                }
                                                passageVersions [(int)m.PassageId] =
                                                    m.VersionNumber ?? 1;
                                            }
                                            m.S3File = await mediaService.GetNewFileNameAsync(m);
                                            m.AudioUrl = _S3service
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
                                                    ArtifactCategoryId = m.ArtifactCategoryId,
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
                                        }
                                        else
                                            UpdateMediafile(mediafile, m, sourceDate, report);
                                    }
                                }
                                ;
                                break;

                            case "groupmemberships":
                                foreach (ResourceObject ro in lst)
                                {
                                    Groupmembership gm = ResourceObjectToResource(
                                        ro,
                                        new Groupmembership()
                                    );
                                    Groupmembership? grpmem = dbContext.Groupmemberships.Find(
                                        gm.Id
                                    );
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
                                ;
                                break;

                            /*  Local changes to project integrations should just stay local
                            case "projectintegrations":
                                List<ProjectIntegration> pis = jsonApiDeSerializer.DeserializeList<ProjectIntegration>(data);
                                break;
                            */

                            case "passagestatechanges":
                                foreach (ResourceObject ro in lst)
                                {
                                    Passagestatechange psc = ResourceObjectToResource(
                                        ro,
                                        new Passagestatechange()
                                    );
                                    //see if it's already there...
                                    IQueryable<Passagestatechange> dups =
                                        dbContext.Passagestatechanges.Where(c =>
                                                c.PassageId == psc.PassageId
                                                && c.DateCreated == psc.DateCreated
                                                && c.State == psc.State
                                        );
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
                                break;
                            case "intellectualpropertys":
                                foreach (ResourceObject ro in lst)
                                {
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
                                break;
                            case "orgkeytermtargets":
                                foreach (ResourceObject ro in lst)
                                {
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
                                                    OrganizationId = tt.Organization?.Id??0,
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
                                break;
                        }
                    }
                    int ret = await dbContext.SaveChangesNoTimestampAsync();

                    UpdateOfflineIds();
                }
                _ = report.RemoveAll(s => s.Length == 0);

                return new Fileresponse()
                {
                    Message = "[" + string.Join(",", report) + "]",
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
        private async Task<Organization> CreateNewOrg(Organization sourceOrg, User user)
        {
            int tryn = 1;
            string orgname = sourceOrg.Name+"_c"+tryn++.ToString();
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
            dbContext.Organizationmemberships.Add(
                new Organizationmembership
                {
                    OrganizationId = t.Entity.Id,
                    UserId = user.Id,
                    RoleId = dbContext.Roles.First(r => r.Rolename == RoleName.Admin && r.Orgrole).Id,
                });
            //get it again because the slug should be there...
            await dbContext.Entry(t.Entity).ReloadAsync();
            return t.Entity;
        }
        private Project CreateNewProject(Project source, int orgId, User user)
        {
            int tryn = 1;
            string projname = source.Name+"_c"+tryn++.ToString();
            while (dbContext.Projects.FirstOrDefault(x => x.OrganizationId == orgId && x.Name == projname) != null)
            {
                projname = source.Name + "_c" + tryn++.ToString();
            }
            EntityEntry<Group> g = dbContext.Groups.Add(new Group
            {
                Name = "All users of " + projname,
                Abbreviation= "all-users",
                OwnerId= orgId,
                AllUsers = true,
            });
            dbContext.SaveChanges();

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
                    GroupId = g.Entity.Id,
                    SpellCheck = source.SpellCheck,
                });
            dbContext.SaveChanges();
            dbContext.Groupmemberships.Add(
                new Groupmembership
                {
                    GroupId = g.Entity.Id,
                    UserId = user.Id,
                    RoleId = dbContext.Roles.First(r => r.Rolename == RoleName.Admin && r.Grouprole).Id,
                });
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
                Artifactcategory? myc = dbContext.Artifactcategorys.FirstOrDefault(m => m.OrganizationId == null && m.Categoryname == c.Categoryname && !m.Archived);
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

                Artifacttype? myc = dbContext.Artifacttypes.FirstOrDefault(m => m.OrganizationId == null && m.Typename == a.Typename && !m.Archived);
                if (myc == null)
                    throw new Exception("missing type" + a.Typename);

                result.Add(a.Id, myc.Id);
            }
            return result;
        }

        private Dictionary<int, int> CopyOrgworkflowsteps(IList<Orgworkflowstep> lst, int orgId)
        {
            Dictionary<int, Orgworkflowstep> map = new();
            foreach (Orgworkflowstep s in lst)
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
            dbContext.SaveChanges();
            Dictionary<int, int> result = new();
            foreach (KeyValuePair<int, Orgworkflowstep> kvp in map)
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
        private Dictionary<int, int> CopySectionResources(IList<Sectionresource> lst, int projectId,
            Dictionary<int, int> sectionMap, Dictionary<int, int> mediafileMap,
            Dictionary<int, int>? owfsMap, Dictionary<int, int> passageMap)
        {
            Dictionary<int, Sectionresource> map = new();
            foreach (Sectionresource sr in lst)
            {
                EntityEntry<Sectionresource>? t =  dbContext.Sectionresources.Add(
                   new Sectionresource
                   {
                       SequenceNum = sr.SequenceNum,
                       Description = sr.Description,
                       SectionId = sectionMap.GetValueOrDefault(sr.SectionId),
                       MediafileId = sr.MediafileId == null ? null : mediafileMap.GetValueOrDefault(sr.MediafileId?? 0),
                       OrgWorkflowStepId = owfsMap == null ? sr.OrgWorkflowStepId : owfsMap.GetValueOrDefault(sr.OrgWorkflowStepId),
                       PassageId = sr.PassageId == null ? null : passageMap.GetValueOrDefault(sr.PassageId??0),
                       ProjectId = projectId
                   });
                map.Add(sr.Id, t.Entity);
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
            string prefix = plan.Slug + "_";
            for (int ix = start; ix < lst.Count && (dtBail == null || DateTime.Now < dtBail); ix++)
            {
                Mediafile m = lst[ix];
                Mediafile copym = new()
                {
                    PassageId = m.PassageId == null ? null : passageMap.GetValueOrDefault(m.PassageId??0),
                    VersionNumber = Convert.ToBoolean(m.VersionNumber) ? m.VersionNumber : 1,
                    ArtifactTypeId = m.ArtifactTypeId == null ? null : artifacttypeMap?.GetValueOrDefault(m.ArtifactTypeId??0) ?? m.ArtifactTypeId,
                    EafUrl = m.EafUrl,
                    Duration = m.Duration,
                    ContentType = m.ContentType,
                    //AudioQuality = m.AudioQuality,
                    //TextQuality = m.TextQuality,
                    Transcription = m.Transcription,
                    PlanId = plan.Id,
                    OriginalFile = m.OriginalFile,
                    Filesize = m.Filesize,
                    Position = m.Position,
                    Segments = m.Segments,
                    Languagebcp47 = m.Languagebcp47,
                    Link = Convert.ToBoolean(m.Link),
                    PerformedBy = m.PerformedBy,
                    ReadyToShare = false,
                    ArtifactCategoryId = m.ArtifactCategoryId == null ? null : sameOrg ? m.ArtifactCategoryId : artifactcategoryMap?.GetValueOrDefault(m.ArtifactCategoryId??0),
                    ResourcePassageId = m.ResourcePassageId == null ? null : passageMap.GetValueOrDefault(m.ResourcePassageId??0),
                    RecordedbyUser = sameOrg ? m.RecordedbyUser : CurrentUser(),
                    OfflineId = "",
                    SourceMediaId = map.GetValueOrDefault(m.SourceMediaId??0)?.Id,
                    SourceSegments = m.SourceSegments,
                    SourceMediaOfflineId = "",
                    Transcriptionstate = m.Transcriptionstate,
                    Topic = m.Topic,
                };
                copym.S3File = mediaService.GetNewFileNameAsync(copym, prefix).Result;
                EntityEntry<Mediafile>? t =  dbContext.Mediafiles.Add(copym);
                map.Add(m.Id, t.Entity);
                //we have to save after every one because we may have a link to previous mediafiles here
                dbContext.SaveChanges();
                if (newProjId != null)
                {
                    SaveId("mediafiles", m.Id, t.Entity.Id, newProjId??0);
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
                _ = dbContext.Comments.Add(
                    new Comment
                    {
                        OfflineId = "",
                        OfflineMediafileId = "",
                        OfflineDiscussionId ="",
                        DiscussionId = discussionMap.GetValueOrDefault(c.DiscussionId??0),
                        CommentText = c.CommentText,
                        MediafileId = c.MediafileId == null ? null : mediafileMap.GetValueOrDefault(c.MediafileId??0),
                        Visible = c.Visible,
                    }
                );
            }
        }

        private Dictionary<int, int>? GetArtifactCategoryMap (bool sameOrg, int newProjId) {
            if (sameOrg)
                return null;
            if (ArtifactCategoryMap == null)
                ArtifactCategoryMap = GetMap("artifactcategorys", newProjId);
            return ArtifactCategoryMap;
        }
        private Dictionary<int, int>? GetOrgworkflowstepMap(bool sameOrg, int newProjId)
        {
            if (sameOrg) return null;
            if (OrgworkflowstepMap == null)
                OrgworkflowstepMap = GetMap("orgworkflowsteps", newProjId);
            return OrgworkflowstepMap;
        }
        private Dictionary<int, int> GetSectionMap(int newProjId)
        {
            if (SectionMap == null)
                SectionMap = GetMap("sections", newProjId);
            return SectionMap;
        }
        private Dictionary<int, int> GetPassageMap(int newProjId)
        {
            if (PassageMap == null)
                PassageMap = GetMap("passages", newProjId);
            return PassageMap;
        }
        private Dictionary<int, int> GetMediafileMap(int newProjId)
        {
            if (MediafileMap == null)
                MediafileMap = GetMap("mediafiles", newProjId);
            return MediafileMap;
        }
        private Dictionary<int, int> GetDiscussionMap(int newProjId)
        {
            if (DiscussionMap == null)
                DiscussionMap = GetMap("discussions", newProjId);
            return DiscussionMap;
        }
        private Dictionary<int, int> GetSectionResourceMap(int newProjId)
        {
            if (SectionResourceMap == null)
                SectionResourceMap = GetMap("sectionresources", newProjId);
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
                    org = await CreateNewOrg(org, currentuser);
                project = CreateNewProject(sourceproject, org.Id, currentuser);
                plan = await CreateNewPlan(sourceplans.First(), project, currentuser);
                newProjId = project.Id;
                SaveId("organizations", oldOrg, org.Id, newProjId);
                SaveId("plans", sourceplans.First().Id, plan.Id, newProjId);
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
                Plan? tmpPlan = dbContext.Plans.Where(p => p.Id == GetSingleId("plans",newProjId)).FirstOrDefault();
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
                        case "artifactcategorys":
                            if (!sameOrg)
                            {
                                ArtifactCategoryMap = CopyArtifactCategorys(dbContext.Artifactcategorys.Where(ac => ac.OrganizationId == null || ac.OrganizationId == sourceproject.OrganizationId).ToList(), org.Id);
                                SaveMap(ArtifactCategoryMap, name, newProjId);
                            }
                            ix++;
                            break;

                        case "intellectualpropertys":
                            if (!sameOrg)
                            {
                                //copy but change the organization to current org
                                CopyIP(OrgIPs(sourceOrg).ToList(), org.Id, GetMediafileMap(newProjId));
                            }
                            ix++;
                            break;

                        case "orgworkflowsteps":
                            if (!sameOrg)
                            {
                                OrgworkflowstepMap = CopyOrgworkflowsteps(dbContext.Orgworkflowsteps.Where(s => s.OrganizationId == sourceproject.OrganizationId).ToList(), org.Id);
                                SaveMap(OrgworkflowstepMap, name, newProjId);
                            }
                            ix++;
                            break;

                        case "sections":
                            SectionMap = CopySections(sourcesections.ToList(), sameOrg, plan.Id, currentuser);
                            SaveMap(SectionMap, name, newProjId);
                            ix++;
                        break;

                        case "passages":
                            //save these for sectionresources next
                            PassageMap = CopyPassages(sourcepassages.ToList(), GetSectionMap(newProjId), GetOrgworkflowstepMap(sameOrg, newProjId));
                            SaveMap(PassageMap, name, newProjId);
                            ix++;
                            break;

                        case "sectionresources":
                            SectionResourceMap = CopySectionResources(sectionresources.ToList(), project.Id, GetSectionMap(newProjId), GetMediafileMap(newProjId), GetOrgworkflowstepMap(sameOrg, newProjId), GetPassageMap(newProjId));
                            SaveMap(SectionResourceMap, name, newProjId);
                            ix++;
                            break;

                        case "sectionresourceusers":
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

                        case "mediafiles":
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

                        case "passagestatechanges":
                            CopyPassagestatechanges(sourcepassages.Join(dbContext.Passagestatechanges,
                                                                        p => p.Id, psc => psc.PassageId,
                                                                        (p, psc) => psc
                                                                    ).ToList(), GetPassageMap(newProjId));
                            ix++;
                            break;

                        case "discussions":
                            DiscussionMap = CopyDiscussions(PlanDiscussions(myMedia).ToList(), sameOrg, GetArtifactCategoryMap(sameOrg, newProjId), GetMediafileMap(newProjId), GetOrgworkflowstepMap(sameOrg, newProjId));
                            SaveMap(DiscussionMap, name, newProjId);
                            ix++;
                            break;

                        case "comments":
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
            bool sameOrg = false;
            Project? sourceproject = GetFileProject(archive);
#pragma warning disable CS8604 // Possible null reference argument.

            sameOrg = !neworg && sourceproject != null;
            if (sameOrg)
            {
                int orgid = sourceproject?.OrganizationId??0;
                org = dbContext.Organizations.FirstOrDefault(o => o.Id == orgid) ?? new Organization();
            }
            try
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!entry.FullName.StartsWith("data"))
                        continue;
                    string name = Path.GetFileNameWithoutExtension(entry.Name[2..]);
                    string? json = new StreamReader(entry.Open()).ReadToEnd();
                    Document? doc = JsonSerializer.Deserialize<Document>(
                        json,
                        options.SerializerReadOptions
                    );
                    IList<ResourceObject>? lst = doc?.Data.ManyValue;
                    if (doc == null || lst == null)
                        continue;
                    switch (name)
                    {
                        case "organizations":
                            if (sameOrg)
                                continue;
                            org = await CreateNewOrg(ResourceObjectToResource(lst.First(), new Organization()), currentuser);
                            break;

                        case "artifactcategorys":
                            if (sameOrg)
                                continue;
                            List<Artifactcategory> ac = new();
                            foreach (ResourceObject ro in lst)
                                ac.Add(ResourceObjectToResource(ro, new Artifactcategory()));
                            ArtifactCategoryMap = CopyArtifactCategorys(ac, org.Id);
                            break;

                        case "artifacttypes":
                            if (sameOrg)
                                continue;
                            ArtifactTypesMap = MapArtifactTypes(lst);
                            break;

                        case "intellectualpropertys":
                            //copy but change the organization to current org
                            if (sameOrg)
                                continue;
                            ipLst = lst;
                            continue;

                        case "orgworkflowsteps":
                            if (sameOrg)
                                continue;
                            List<Orgworkflowstep> owlst = new();
                            foreach (ResourceObject ro in lst)
                                owlst.Add(ResourceObjectToResource(ro, new Orgworkflowstep()));
                            OrgworkflowstepMap = CopyOrgworkflowsteps(owlst, org.Id);
                            break;

                        case "projects":
                            project = CreateNewProject(ResourceObjectToResource(lst.First(), new Project()), org.Id, currentuser);
                            break;

                        case "plans":
                            plan = await CreateNewPlan(ResourceObjectToResource(lst.First(), new Plan()), project, currentuser);
                            break;

                        case "sections":
                            List<Section> slst = new();
                            foreach (ResourceObject ro in lst)
                                slst.Add(ResourceObjectToResource(ro, new Section()));
                            SectionMap = CopySections(slst, sameOrg, plan.Id, currentuser);
                            break;

                        case "passages":
                            List<Passage> plst = new();
                            foreach (ResourceObject ro in lst)
                                plst.Add(ResourceObjectToResource(ro, new Passage()));
                            PassageMap = CopyPassages(plst, SectionMap, OrgworkflowstepMap);
                            break;

                        case "sectionresources":
                            srLst = lst;
                            break;

                        case "sectionresourceusers":
                            if (!sameOrg)
                                break;
                            srUserLst = lst;
                            break;

                        case "mediafiles":
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

                        case "passagestatechanges":
                            List<Passagestatechange> psclist = new();
                            foreach(ResourceObject ro in lst)
                                psclist.Add(ResourceObjectToResource(ro, new Passagestatechange()));
                            CopyPassagestatechanges(psclist, PassageMap);
                            break;

                         case "discussions":
                            List<Discussion> dlst = new();
                            foreach (ResourceObject ro in lst)
                                dlst.Add(ResourceObjectToResource(ro, new Discussion()));
                            DiscussionMap = CopyDiscussions(dlst,sameOrg, ArtifactCategoryMap, MediafileMap, OrgworkflowstepMap);
                            break;

                        case "comments":
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
                    SectionResourceMap = CopySectionResources(lst, project.Id, SectionMap, MediafileMap, OrgworkflowstepMap, PassageMap);
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
