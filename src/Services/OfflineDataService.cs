using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Serialization.Response;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Serializers;
using SIL.Transcriber.Services.Contracts;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Utility.Extensions;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using static SIL.Transcriber.Utility.ResourceHelpers;
using IdMap = System.Collections.Generic.Dictionary<string, int>;

namespace SIL.Transcriber.Services
{
    public class OfflineDataService(
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

        ) : IOfflineDataService
    {
        protected readonly AppDbContext dbContext = (AppDbContext)contextResolver.GetContext();
        protected readonly MediafileService mediaService = MediaService;
        protected CurrentUserRepository CurrentUserRepository { get; } = currentUserRepository;

        readonly private IS3Service _S3Service = service;
        readonly private ISQSService _SQSService = sqsService;
        private const string ImportFolder = "imports";
        private const string ExportFolder = "exports";

        protected ILogger<OfflineDataService> Logger { get; set; } = loggerFactory.CreateLogger<OfflineDataService>();

        private readonly IResourceGraph _resourceGraph = resourceGraph;
        private readonly IResourceDefinitionAccessor _resourceDefinitionAccessor = resourceDefinitionAccessor;
        private readonly IMetaBuilder _metaBuilder = metaBuilder;
        private readonly IJsonApiOptions _options = options;
        readonly private HttpContext? HttpContext = httpContextAccessor.HttpContext;

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
            {Tables.OrganizationSchemes,'C'},
            {Tables.OrganizationSchemeSteps,'D'},
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
            {Tables.Bibles, 'I' }, //because it has mediafiles in it
            {Tables.OrgKeyTermTargets,'I'},
            {Tables.SectionResources,'I'},
            {Tables.Discussions,'I'},
            {Tables.IntellectualPropertys,'I'},
            {Tables.SharedResources, 'I' },
            {Tables.OrganizationBibles, 'I' },
            {Tables.Comments,'J'},
            {Tables.SectionResourceUsers,'J'},
            {Tables.SharedResourceReferences, 'J' }
        };
        IdMap? ActivityStateMap = null;
        IdMap? IntegrationMap = null;
        IdMap? PassageTypeMap = null;
        IdMap? PlanTypeMap = null;
        IdMap? ProjectTypeMap = null;
        IdMap? RoleMap = null;
        IdMap? WorkflowStepMap = null;
        IdMap? ArtifactCategoryMap = null;
        IdMap? ArtifactTypesMap = null;
        IdMap? OrgworkflowstepMap = null;
        IdMap? SectionMap = null;
        IdMap? PassageMap  = null;
        IdMap? MediafileMap = null;
        IdMap? DiscussionMap = null;
        IdMap? SectionResourceMap = null;
        IdMap? UserMap = null;

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
        private void AddUsersToOrg(int orgid, string mapKey)
        {
            IdMap users = GetUserMap(mapKey);
            //get the distinct values and add them to the orgmems
            IEnumerable<int> distinctValues = users.Values.Distinct();
            foreach (int u in distinctValues)
            {
                if (dbContext.Organizationmemberships.FirstOrDefault(om => om.UserId == u && om.OrganizationId == orgid) == null)
                {
                    Organizationmembership om = new ()
                    {
                        UserId = u,
                        OrganizationId = orgid
                    };

                    dbContext.Organizationmemberships.Add(om);
                }
            }
            dbContext.SaveChanges();
        }
        private void InviteUserToOrg(int orgid, string email)
        {
            Invitation i = new ()
            {
                RoleId = dbContext.Roles.First(r => r.Rolename == RoleName.Admin && r.Orgrole).Id,
                OrganizationId = orgid,
                Email = email,
                AllUsersRoleId = dbContext.Roles.First(r => r.Rolename == RoleName.Admin && r.Grouprole).Id,
                LoginLink = "https://app-dev.audioprojectmanager.org",
                InvitedBy=CurrentUser()?.Email ?? "sara_hentzel@sil.org"
            };
            dbContext.Invitations.Add(i);
            dbContext.SaveChanges();
        }
        private void AddUsersToGroup(int grpid, string mapKey)
        {
            IdMap users = GetUserMap(mapKey);
            //get the distinct values and add them to the orgmems
            IEnumerable<int> distinctValues = users.Values.Distinct();
            foreach (int u in distinctValues)
            {
                if (dbContext.Groupmemberships.FirstOrDefault(om => om.UserId == u && om.GroupId == grpid) == null)
                {
                    Groupmembership om = new ()
                    {
                        UserId = u,
                        GroupId = grpid
                    };

                    dbContext.Groupmemberships.Add(om);
                }
            }
            dbContext.SaveChanges();
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
                    url = css[start..end];
                    string fontFile = url[(url.LastIndexOf('/') + 1)..];
                    url = bucket + fontFile;
                    _ = AddStreamEntry(zipArchive, url, "fonts/", fontFile);
                    css = css[..(start + 1)] + fontFile + css[end..];
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
                    if (data.IndexOf('|') > 0)
                    {
                        err = data[(data.IndexOf('|') + 1)..];
                        data = data[..data.IndexOf('|')];
                    }
                    bool media = data.Contains(" media");
                    if (media)
                        data = data[..data.IndexOf(" media")];
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
                .UploadFileAsync(ms, true, fileName, ExportFolder)
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
            Dictionary<string, List<string>> scopes = [];
            List<string> formats = [];

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
            root.languages[0].tag = project.Language;
            root.languages[0].name.en = project.LanguageName;

            mediafiles.ForEach(m => {
                //get stored book and ref out of audioquality
                string[] split = (m.AudioQuality ?? "|").Split("|");
                string book = split[0];
                string reference = split[1];
                if (!scopes.ContainsKey(book))
                    scopes.Add(book, []);
                scopes[book].Add(reference);
                string ext = Path.GetExtension(m.AudioUrl ?? "").TrimStart('.');
                if (!formats.Contains(ext))
                    formats.Add(ext);
                root.ingredients[m.AudioUrl] = new JObject();
                if (mimeMap.TryGetValue(ext, out string? value))
                    root.ingredients[m.AudioUrl].mimeType = value;
                root.ingredients[m.AudioUrl].size = m.Filesize;
                string scopestr = string.Format("{{[{0}]:[{1}]}}", book, reference);
                root.ingredients[m.AudioUrl].scope = new JObject();
                root.ingredients[m.AudioUrl].scope[book] = JToken.FromObject(
                    new string[] { reference }
                );
            });
            for (int n = 0; n < formats.Count; n++)
            {
                string name = "format" + (n + 1).ToString();
                root.type.flavorType.flavor.formats[name] = new JObject();
                root.type.flavorType.flavor.formats[name].compression = formats[n];
            }
            foreach (KeyValuePair<string, List<string>> item in scopes)
            {
                root.type.flavorType.currentScope[item.Key] = JToken.FromObject(
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
            List<Mediafile>? ipMedia = [.. ip.Join(dbContext.Mediafiles, ip => ip.ReleaseMediafileId, m => m.Id, (ip, m) => m)];

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
            return value?.ToString() ?? "";
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
                        pref = section.Name ?? "S" + section.Sequencenum.ToString().PadLeft(3, '0');
                    else if (!flat)
                        pref = (nameTemplate.Contains("{SECT}") ? "" : "S" + section.Sequencenum.ToString().PadLeft(3, '0')) + "_P" + passage.Sequencenum.ToString().PadLeft(3, '0');
                }
                name = name.Replace("{REF}", pref);
            }
            if (name.Replace("_", "") == "" || name.StartsWith("_v"))
                name = "S" + section.Sequencenum.ToString().PadLeft(3, '0') + (flat ? "" : "_P" + passage.Sequencenum.ToString().PadLeft(3, '0'));
            return FileName.CleanFileName(name) + Path.GetExtension(m.S3File);
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
                FileName.CleanFileName(project.Name + artifactType),
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
                    List<Mediafile> mediafiles = [.. dbContext.Mediafiles.Where(x => (idList ?? "").Contains("," + x.Id.ToString() + ","))];
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
                FileName.CleanFileName(project.Name),
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
                    List<Mediafile> mediaList = [.. dbContext.Mediafiles.Where(x => (idList ?? "").Contains("," + x.Id.ToString() + ","))];
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
        private IQueryable<Mediafile> PlanMedia(IQueryable<Plan> plans)
        {
            return plans
                        .Join(dbContext.MediafilesData, p => p.Id, m => m.PlanId, (p, m) => m)
                        .Where(x => !x.Archived);
        }
        private IQueryable<Sectionresource> SectionResources(IQueryable<Section> sections)
        {
            return dbContext.Sectionresources
                        .Join(sections, r => r.SectionId, s => s.Id, (r, s) => r)
                        .Where(x => !x.Archived);
        }
        private IEnumerable<Mediafile> CategoryMedia(IQueryable<Artifactcategory> categories)
        {
            return dbContext.Mediafiles
                        .Join(categories, m => m.Id, c => c.TitleMediafileId, (m, c) => m)
                        .Where(x => !x.Archived);
        }
        private IEnumerable<Mediafile> PlanSourceMedia(IQueryable<Sectionresource> sectionresources)
        {
            //get the mediafiles associated with section resources
            IQueryable<Mediafile> resourcemediafiles = dbContext.Mediafiles
                        .Join(sectionresources, m => m.Id, r => r.MediafileId, (m, r) => m)
                        .Where(x => !x.Archived);

            //now get any shared resource mediafiles associated with those mediafiles
            IEnumerable<Mediafile> sourcemediafiles = [.. dbContext.Mediafiles
                        .Join(
                            resourcemediafiles,
                            m => m.PassageId,
                            r => r.ResourcePassageId,
                            (m, r) => m
                        )
                        .Where(x => x.ReadyToShare && !x.Archived)];
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
        private static IEnumerable<Mediafile> AttachedMedia(List<Mediafile> myMedia)
        {
            return myMedia
                        .Where(x => (x.PassageId != null || x.ArtifactTypeId != null) &&
                            x.ResourcePassageId == null && !x.Archived && x.ContentType != "text/markdown").Distinct();

        }
        private IQueryable<Discussion> PlanDiscussions(IQueryable<Mediafile> myMedia)
        {
            return dbContext.Discussions
                        .Join(myMedia, d => d.MediafileId, m => m.Id, (d, m) => d)
                        .Where(x => !x.Archived);
        }
        private List<Mediafile> ProjectMedia(
            IQueryable<Orgkeytermtarget> orgkeytermtargets,
            IQueryable<Artifactcategory> categories,
            IQueryable<Sectionresource> sectionresources,
            IQueryable<Intellectualproperty> ip,
            IQueryable<Plan> plans,
            IQueryable<Note> supportingNotes,
            IQueryable<Bible> bibles)
        {
            IQueryable<Mediafile> okttmedia = orgkeytermtargets.Join(dbContext.MediafilesData, o => o.MediafileId, m => m.Id, (o, m) => m);
            IQueryable<Mediafile> ipmedia = ip.Join(dbContext.MediafilesData, ip => ip.ReleaseMediafileId, m=> m.Id, (ip, m) => m).Where(x => !x.Archived);
            IEnumerable<Mediafile> sourcemediafiles = PlanSourceMedia(sectionresources);
            IEnumerable<Mediafile> categorymediafiles =  CategoryMedia(categories);
            IQueryable<Mediafile> sharedNoteMedia = supportingNotes.Join(
                                          dbContext.MediafilesData, n => n.MediafileId, m => m.Id, (n, m) => m);
            IQueryable<Mediafile> bibleMedia = bibles.Join(
                                          dbContext.MediafilesData, b => b.BibleMediafileId, m => m.Id, (b, m) => m);
            IQueryable<Mediafile> bibleisoMedia = bibles.Join(
                                          dbContext.MediafilesData, b => b.IsoMediafileId, m => m.Id, (b, m) => m);

            List<Mediafile> myMedia = [.. PlanMedia(plans).ToList()
                                .Concat([.. okttmedia])
                                .Concat([.. ipmedia])
                                .Concat([.. sourcemediafiles])
                                .Concat([.. categorymediafiles])
                                .Concat([.. sharedNoteMedia])
                                .Concat([.. bibleMedia])
                                .Concat([.. bibleisoMedia])
                                .Distinct()];
            return myMedia;
        }
        public Fileresponse ExportProjectPTF(int projectId, int start)
        {
            const int LAST_ADD = 25;
            const string ext = ".ptf";
            int startNext = start;
            //give myself 15 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(15);

            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == projectId);
            Project project = projects.First();
            string fileName = string.Format(
                "APM{0}_{1}_{2}",
                FileName.CleanFileName(project.Name),
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

                IQueryable<Intellectualproperty>? ip = OrgIPs(orgs);
                if (start == 0)
                {
                    Dictionary<string, string> fonts = new()
                    {
                        { "Charis SIL", "" }
                    };
                    DateTime exported = AddCheckEntry(
                        zipArchive,
                        dbContext.Currentversions.FirstOrDefault()?.SchemaVersion ?? 8
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
                    List<Organization> orgList = [.. orgs];

                    AddOrgLogos(zipArchive, orgList);
                    AddJsonEntry(zipArchive, Tables.Organizations, orgList);

                    //groups
                    IQueryable<Group> groups = dbContext.GroupsData.Join(
                        orgs,
                        g => g.OwnerId,
                        o => o.Id,
                        (g, o) => g
                    );
                    List<Groupmembership> gms = [.. groups
                        .Join(
                            dbContext.Groupmemberships,
                            g => g.Id,
                            gm => gm.GroupId,
                            (g, gm) => gm
                        )
                        .Where(gm => !gm.Archived)];
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
                            fonts[font] = ""; //add it if it's not there
                    }
                    foreach (
                        string? font in projects
                            .Where(p => p.DefaultFont != null)
                            .Select(p => p.DefaultFont)
                    )
                    {
                        if (font != null)
                            fonts[font] = ""; //add it if it's not there
                    }
                    AddFonts(zipArchive, fonts.Keys);
                    //users
                    List<User> userList = [..users];
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
                    AddJsonEntry(zipArchive, Tables.Projects, projects.ToList());
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
                                    dbContext.ProjectintegrationsData,
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
                        .Join(dbContext.PlansData, p => p.Id, pl => pl.ProjectId, (p, pl) => pl)
                        .Where(x => !x.Archived);
                    IQueryable<Artifactcategory> categories = dbContext.Artifactcategorys.Where(a =>
                                        (   a.OrganizationId == null
                                            || a.OrganizationId == project.OrganizationId
                                        ) && !a.Archived);
                    IQueryable<VWProject> sharednotes = dbContext.VWProjects.Where(x => x.ProjectId == project.Id && x.SharedResourceId != null);
                    IQueryable<Note> supportingNotes = dbContext.Notes
                        .Join(sharednotes, n => n.ResourceId, sn => sn.SharedResourceId, (n, sn) => n);

                    IQueryable<Orgkeytermtarget> orgkeytermtargets = dbContext.OrgKeytermTargetsData.Where(
                                    a => (a.OrganizationId == project.OrganizationId) && !a.Archived
                                );
                    IQueryable<Section> sections = plans
                        .Join(dbContext.SectionsData, p => p.Id, s => s.PlanId, (p, s) => s)
                        .Where(x => !x.Archived);
                    IQueryable<Sectionresource> sectionresources = SectionResources(sections);
                    IQueryable<Bible>  orgBibles = dbContext.Organizationbibles.Where(om => om.OrganizationId == project.OrganizationId && !om.Archived)
                        .Join(dbContext.BiblesData.Where(b=>!b.Archived), ob => ob.BibleId, b => b.Id, (ob, b) => b);
                    List<Mediafile> mediafiles = ProjectMedia(orgkeytermtargets, categories,
                        sectionresources, ip,plans, supportingNotes, orgBibles);
                    List<int>  planIds = [..mediafiles.Select(m => m.PlanId).Distinct()];
                    IQueryable<Plan> supportingPlans = dbContext.PlansData.Where(p => planIds.Contains(p.Id)) ;
                    List<int> projIds = [.. supportingPlans.Where(p => p.ProjectId != project.Id).Select(p => p.ProjectId)];
                    IQueryable<Project> supportingProjects = dbContext.ProjectsData.Where(p => projIds.Contains(p.Id));
                    if (
                        !CheckAdd(
                            2,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "supportingprojects",
                            supportingProjects.ToList()
                        )
                    )
                        break;
                    if (
                        !CheckAdd(
                            3,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Plans,
                            plans.Concat(supportingPlans).ToList()
                        )
                    )
                        break;
                    //sections

                    IQueryable<Section> supportingSections = dbContext.SectionsData.Join(
                        supportingNotes, s => s.Id, n => n.SectionId, (s,n) => s);
                    IQueryable<Passage> passages = sections
                        .Join(dbContext.PassagesData, s => s.Id, p => p.SectionId, (s, p) => p)
                        .Where(x => !x.Archived);
                    IQueryable<Passage>  supportingPassages = dbContext.PassagesData.Join(
                                               supportingNotes, p => p.Id, n => n.PassageId, (p,n) => p);
                    if (
                        !CheckAdd(
                            4,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Sections,
                            sections.Concat(supportingSections).ToList()
                        )
                    )
                        break;
                    //passages
                    if (
                        !CheckAdd(
                            5,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Passages,
                            passages.ToList().Concat(supportingPassages).ToList()
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
                            6,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.PassageStateChanges,
                            passagestatechanges.ToList()
                        )
                    )
                        break;

                    if (
                        !CheckAdd(
                            7,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Mediafiles,
                            mediafiles.OrderBy(m => m.Id).ToList()
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
                            categories.ToList()
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
                    IQueryable<Discussion> discussions = PlanDiscussions(PlanMedia(plans));
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
                            orgkeytermtargets.ToList()
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
                    //organizationschemes
                    IQueryable<Organizationscheme> schemes =dbContext.Organizationschemes
                                .Where(s => s.OrganizationId == project.OrganizationId && !s.Archived);
                    if (
                       !CheckAdd(
                           20,
                           dtBail,
                           ref startNext,
                           zipArchive,
                           Tables.OrganizationSchemes,
                           schemes.ToList()
                       )
                   )
                        break;
                    if (!CheckAdd(
                            21,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.OrganizationSchemeSteps,
                            dbContext.Organizationschemesteps
                                .Join(schemes, ss => ss.OrganizationschemeId, s => s.Id, (ss, s) => ss)
                                .Where(ss => !ss.Archived)
                                .ToList()
                        )
                    )
                        break;
                    if (!CheckAdd(
                            22,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Graphics,
                             dbContext.GraphicsData.Where(om => om.OrganizationId == project.OrganizationId && !om.Archived).ToList()
                        )
                    )
                        break;
                    if (!CheckAdd(
                            23,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.OrganizationBibles,
                             dbContext.OrganizationbiblesData.Where(om => om.OrganizationId == project.OrganizationId && !om.Archived).ToList()
                        )
                    )
                        break;
                    if (!CheckAdd(
                            24,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            Tables.Bibles,
                             orgBibles.ToList()
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
                    AddAttachedMedia(zipArchive, [.. AttachedMedia(mediafiles)], null);
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
                FileName.CleanFileName(Path.GetFileNameWithoutExtension(sFile)),
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
            List<string> report = [];
            List<string> errors = [];
            string startIndex = "0/0";
            for (int ix = fileIndex; ix < archive.Entries.Count; ix++)
            {
                ZipArchiveEntry entry = archive.Entries[ix];
                ZipArchive zipEntry = new(entry.Open());
                Fileresponse fr = await ProcessImportFileAsync(zipEntry, 0, entry.Name, start, dtBail);
                if (fr.Status is HttpStatusCode.OK or HttpStatusCode.PartialContent)
                { //remove beginning and ending brackets
                    string msg = fr.Message.StartsWith('[')
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
        public async Task<Fileresponse> ImportCopyFileIntoOrgAsync(int org, string sFile, int start, string? mapKey)
        {
            S3Response response = await _S3Service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return FileNotFound(sFile);
            ZipArchive archive = new(response.FileStream);
            return await ProcessImportCopyFileIntoOrgAsync(archive, org, sFile, start, mapKey);

        }
        public async Task<Fileresponse> ImportCopyProjectAsync(bool neworg, int projectId, int start, string? newProjId)
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
            string contentType = string.Concat("application/", (Path.GetExtension(sFile))[1..]);
            return new Fileresponse()
            {
                Message = msg,
                FileURL = sFile,
                Status = status,
                ContentType = contentType,
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
            f ??= archive.Entries
                .Where(e => e.Name == "media\\" + m.OriginalFile)
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
            List<Comment> comments = [.. dbContext.Comments.Where(
                c => c.OfflineMediafileId != null
            )];
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
            comments = [.. dbContext.Comments.Where(
                c => c.DiscussionId == null && c.OfflineDiscussionId != null
            )];
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
            List<Discussion> discussions = [.. dbContext.Discussions.Where(
                d => d.MediafileId == null && d.OfflineMediafileId != null
            )];
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

            List<Mediafile> mediafiles = [.. dbContext.Mediafiles.Where(
                c => c.SourceMediaId == null && c.SourceMediaOfflineId != null
            )];
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
            List<Intellectualproperty> ips = [.. dbContext.IntellectualPropertys.Where(
                c => c.OfflineMediafileId != null
            )];
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
            List<Orgkeytermtarget> ktts = [.. dbContext.Orgkeytermtargets.Where(
                c => c.OfflineMediafileId != null
            )];
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
                Nullable.Compare(a.DateUpdated, b.DateUpdated);
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
                existing.GroupId = CheckValidId(importing.GroupId);
                existing.Resolved = importing.Resolved;
                existing.UserId = CheckValidId(importing.UserId);
                existing.MediafileId = CheckValidId(importing.MediafileId);
                existing.OfflineMediafileId = importing.OfflineMediafileId;
                existing.LastModifiedBy = CheckValidId(importing.LastModifiedBy);
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
        private static bool IsNumber(string? s)
        {
            return int.TryParse(s, out _);
        }

        private TResource ResourceObjectToResource<TResource>(ResourceObject ro, TResource s, string mapKey)
            where TResource : class, IIdentifiable
        {
            ResourceType resourceType = _resourceGraph.GetResourceType(typeof(TResource));
            IReadOnlyCollection<AttrAttribute>? attrs = resourceType.Attributes;
            IReadOnlyCollection<RelationshipAttribute>? rels = resourceType.Relationships;

            if (IsNumber(ro.Id))
                s.StringId = ro.Id;


            if (ro.Attributes != null)
                foreach (KeyValuePair<string, object?> row in ro.Attributes)
                {
                    AttrAttribute? myTypeAttribute = attrs.FirstOrDefault(
                        a => a.PublicName == row.Key
                    );
                    myTypeAttribute ??= attrs.FirstOrDefault(a => a.PublicName == row.Key.CameltoKebab());

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

            AttrAttribute? offlineIdAttribute = attrs.FirstOrDefault(
                        a => a.PublicName == "offline-id");
            offlineIdAttribute?.SetValue(s, ro.Id);

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
                    myTypeRelationship ??= rels.FirstOrDefault(
                        r => r.PublicName == row.Key.CameltoKebab()
                    );
                    if (myTypeRelationship != null)
                    {
                        string oldId = row.Value?.Data.SingleValue?.Id??"";
                        bool isNum = int.TryParse(oldId, out int oldid);
                        int id = 0;
                        if (oldId != "")
                            id = GetMappedId(myTypeRelationship.Property.PropertyType.Name, mapKey, oldId) ?? 0;
                        if (isNum && id == 0)
                            id = oldid;
                        try
                        {
                            object? p = null;
                            if (id > 0)
                                p = dbContext.Find(myTypeRelationship.Property.PropertyType, id);

                            if (p != null)
                                myTypeRelationship.SetValue(s, p);
                        }
                        catch (Exception e)
                        {
                            Logger.LogError("unable to find {r} with id {id} oldid {oldid} {e}", myTypeRelationship.PublicName, id, oldId, e);
                        }
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
            while (snake.IndexOf('-') > 0)
            {
                int ix = snake.IndexOf('-');
                snake = string.Concat(
                    snake.AsSpan(0, ix),
                    snake.Substring(ix + 1, 1).ToUpper(),
                    snake.AsSpan(ix + 2)
                );
            }
            return string.Concat(snake[..1].ToUpper(), snake.AsSpan(1));
        }
        private Project? ReadFileProject(ZipArchive archive, string mapKey)
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

            return fileproject == null ? null : ResourceObjectToResource(fileproject, new Project(), mapKey);
        }
        private Project? GetFileProject(ZipArchive archive, string mapKey)
        {
            Project? fileproject = ReadFileProject(archive, mapKey);
            return fileproject == null ? null : dbContext.Projects.Find(fileproject.Id);
        }
        private Organization? ReadFileOrganization(ZipArchive archive, string mapKey)
        {
            IJsonApiOptions options = new JsonApiOptions();
            ZipArchiveEntry? orgsEntry = archive.GetEntry("data/B_organizations.json");
            if (orgsEntry == null)
                return null;
            string json = new StreamReader(orgsEntry.Open()).ReadToEnd();

            Document? doc = JsonSerializer.Deserialize<Document>(
                    json,
                    options.SerializerReadOptions
                );
            ResourceObject? fileorg = doc?.Data.SingleValue ?? (doc?.Data.ManyValue?[0]);

            return fileorg == null ? null : ResourceObjectToResource(fileorg, new Organization(), mapKey);
        }

        private int UpdateUsers(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail, string mapKey)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId) //at least do one
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                User u = ResourceObjectToResource(ro, new User(), mapKey);
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
                    user.HotKeys = u.HotKeys;
                    /* TODO: figure out if the avatar needs uploading */
                    _ = dbContext.Users.Update(user);
                }
            }
            return -1;
        }
        private int UpdateSections(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail, string mapKey)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Section s = ResourceObjectToResource(ro, new Section(), mapKey);
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
        private int UpdatePassages(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail, int currentuser, string mapKey)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Passage p = ResourceObjectToResource(ro, new Passage(), mapKey);
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
        private int CreateOrUpdateDiscussions(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail, string mapKey)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Discussion d = ResourceObjectToResource(ro, new Discussion(),  mapKey);
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
                                MediafileId = CheckValidId(d.MediafileId),
                                OfflineId = d.OfflineId,
                                OfflineMediafileId = d.OfflineMediafileId,
                                OrgWorkflowStepId = d.OrgWorkflowStepId,
                                GroupId = d.Group?.Id,
                                Resolved = d.Resolved,
                                Segments = d.Segments,
                                Subject = d.Subject,
                                UserId = d.User?.Id,
                                LastModifiedBy = CheckValidId(d.LastModifiedBy),
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
        private int CreateOrUpdateComments(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail, string mapKey)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Comment c = ResourceObjectToResource(ro, new Comment(), mapKey);
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
        private static int? CheckValidId(int? id)
        {
            return id <= 0 ? null : id;
        }
        private async Task<int> CreateOrUpdateMediafiles(List<Mediafile> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail, ZipArchive archive)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
                    return lastIndex;
                Mediafile m = lst[lastIndex];
                Dictionary<int, int> passageVersions = [];
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
                            passageVersions[(int)m.PassageId] = m.VersionNumber ?? 1;
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
                                ArtifactTypeId = CheckValidId(m.ArtifactTypeId),
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
                                PassageId = CheckValidId(m.PassageId),
                                PerformedBy = m.PerformedBy,
                                PlanId = m.PlanId,
                                Position = m.Position,
                                ReadyToShare = m.ReadyToShare,
                                // RecordedbyuserId = m.RecordedbyuserId,
                                RecordedbyUser = m.RecordedbyUser,
                                ResourcePassageId = CheckValidId(m.ResourcePassageId),
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
        private int UpdateGroupMemberships(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail, string mapKey)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];

                Groupmembership gm = ResourceObjectToResource(ro,new Groupmembership(), mapKey);
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
        private int CreatePassageStateChanges(IList<ResourceObject> lst, int startId, DateTime dtBail, string mapKey)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];

                Passagestatechange psc = ResourceObjectToResource(ro, new Passagestatechange(), mapKey);
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
        private int CreateIPs(IList<ResourceObject> lst, int startId, DateTime dtBail, string mapKey)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];

                Intellectualproperty ip = ResourceObjectToResource(ro, new Intellectualproperty(), mapKey);
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
                                LastModifiedBy = CheckValidId(ip.LastModifiedBy),
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
        private int CreateOrgKeyTermTargets(IList<ResourceObject> lst, int startId, DateTime dtBail, string mapKey)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Orgkeytermtarget tt = ResourceObjectToResource(ro, new Orgkeytermtarget(), mapKey);
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
            const string mapKey = "";
            IJsonApiOptions options = new JsonApiOptions();
            List<string> report = [];
            List<string> deleted = [];
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
                    project = GetFileProject(archive, mapKey);

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
                            startId = UpdateUsers(lst, startId, sourceDate, report, dtBail, mapKey);
                            break;

                        case Tables.Sections:
                            startId = UpdateSections(lst, startId, sourceDate, report, dtBail, mapKey);
                            break;

                        case Tables.Passages:
                            int currentuser = CurrentUser()?.Id ?? 0;
                            startId = UpdatePassages(lst, startId, sourceDate, report, dtBail, currentuser, mapKey);
                            break;

                        case Tables.Discussions:
                            startId = CreateOrUpdateDiscussions(lst, startId, sourceDate, report, dtBail, mapKey);
                            break;

                        case Tables.Comments:
                            startId = CreateOrUpdateComments(lst, startId, sourceDate, report, dtBail, mapKey);
                            break;

                        case Tables.Mediafiles:
                            List<Mediafile> sorted = [];
                            foreach (ResourceObject ro in lst)
                            {
                                sorted.Add(ResourceObjectToResource(ro, new Mediafile(), mapKey));
                            }
                            sorted.Sort(CompareMediafilesByArtifactTypeVersionDate);
                            startId = await CreateOrUpdateMediafiles(sorted, startId, sourceDate, report, dtBail, archive);
                            break;

                        case Tables.GroupMemberships:
                            startId = UpdateGroupMemberships(lst, startId, sourceDate, report, dtBail, mapKey);
                            break;

                        /*  Local changes to project integrations should just stay local
                        case "projectintegrations":
                            List<ProjectIntegration> pis = jsonApiDeSerializer.DeserializeList<ProjectIntegration>(data);
                            break;
                        */

                        case Tables.PassageStateChanges:
                            startId = CreatePassageStateChanges(lst, startId, dtBail, mapKey);
                            break;

                        case Tables.IntellectualPropertys:
                            startId = CreateIPs(lst, startId, dtBail, mapKey);
                            break;

                        case Tables.OrgKeyTermTargets:
                            startId = CreateOrgKeyTermTargets(lst, startId, dtBail, mapKey);
                            break;

                        default:
                            startId = -1;
                            break;

                    }
                    start = StartIndex.SetStart(start, ref startId);
                }
                ;
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
        private async Task<Organization> CreateNewOrg(Organization sourceOrg, bool sameName, bool addLang, User user)
        {
            int tryn = 1;
            string lang = "";
            if (addLang && sourceOrg.DefaultParams != null)
            {
                dynamic? x = Newtonsoft.Json.JsonConvert.DeserializeObject(sourceOrg.DefaultParams);
                dynamic? lp = x?.langProps;
                lang = lp?.languageName ?? "";
                if (lang != "")
                    lang = " " + lang;
            }
            string orgname = sourceOrg.Name+lang+(sameName ? "" : "_c"+tryn++).ToString();
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
            dbContext.SaveChanges();
            int tryn = 1;
            string projname = source.Name+(sameName ? "" : "_c"+tryn++).ToString();
            while (dbContext.Projects.FirstOrDefault(x => x.OrganizationId == orgId && x.Name == projname) != null)
            {
                projname = source.Name + "_c" + tryn++.ToString();
            }
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
        private IdMap CopySections(IList<Section> lst, int planId)
        {
            Dictionary<string, Section> map = [];
            foreach (Section s in lst)
            {
                string id = s.StringId ?? s.OfflineId ?? "error";
                if (!map.ContainsKey(id))
                {
                    EntityEntry<Section>? t = dbContext.Sections.Add(
                    new Section
                    {
                        Name = s.Name,
                        PlanId = planId,
                        Sequencenum = s.Sequencenum,
                        //EditorId = sameOrg ?  CheckValidId(s.Editor?.Id) : currentuser.Id,
                        //TranscriberId = sameOrg ? CheckValidId(s.Transcriber?.Id) : currentuser.Id,
                        State = s.State,
                        Level = s.Level,
                        TitleMediafileId = s.TitleMediafileId, // MediafileMap.GetValueOrDefault(s.TitleMediafileId),
                    });
                    map.Add(id, t.Entity);
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Section> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private IdMap CopyPassages(IList<Passage> lst)
        {
            Dictionary<string, Passage> map = [];
            foreach (Passage p in lst)
            {
                string id = p.StringId ?? p.OfflineId ?? "error";
                if (!map.ContainsKey(id))
                {
                    EntityEntry<Passage>? t = dbContext.Passages.Add(
                    new Passage
                    {
                        Sequencenum = p.Sequencenum,
                        Book = p.Book,
                        Reference = p.Reference,
                        Hold = p.Hold,
                        Title = p.Title,
                        SectionId = p.SectionId, // SectionMap.GetValueOrDefault(p.SectionId),
                        StepComplete =  MapStepComplete(p.StepComplete, OrgworkflowstepMap),
                    });
                    map.Add(id, t.Entity);
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Passage> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private IdMap MapUsers(List<User> lst)
        {
            IdMap map = [];
            int currentuser = CurrentUser()?.Id ?? 0;
            foreach (User u in lst)
            {
                User? myu = null;
                if (u.Email != "")
                    myu = dbContext.Users.FirstOrDefault(m => m.Email == u.Email && !m.Archived);
                map.TryAdd(u.StringId ?? u.OfflineId ?? "You're screwed", myu?.Id ?? currentuser);
            }
            return map;
        }
        private IdMap MapActivityStates(List<Activitystate> lst)
        {
            IdMap map = [];
            int done = dbContext.Activitystates.FirstOrDefault(m => m.State == "done")?.Id??0;
            foreach (Activitystate r in lst)
            {
                Activitystate? myr = dbContext.Activitystates.FirstOrDefault(m => m.State.ToLower() == r.State.ToLower());
                map.TryAdd(r.StringId ?? r.OfflineId ?? "You're screwed", myr?.Id ?? done);
            }
            return map;
        }
        private IdMap MapPassageTypes(List<Passagetype> lst)
        {
            IdMap map = [];
            foreach (Passagetype r in lst)
            {
                Passagetype? myr = dbContext.Passagetypes.FirstOrDefault(m => m.Abbrev == r.Abbrev.ToUpper());
                if (myr != null)
                    map.TryAdd(r.StringId ?? r.OfflineId ?? "You're screwed", myr.Id);
            }
            return map;
        }
        private IdMap MapPlanTypes(List<Plantype> lst)
        {
            IdMap map = [];
            int other = dbContext.Plantypes.FirstOrDefault(m => m.Name == "Other")?.Id??0;

            foreach (Plantype r in lst)
            {
                Plantype? myr = dbContext.Plantypes.FirstOrDefault(m => m.Name.ToLower() == r.Name.ToLower());
                map.TryAdd(r.StringId ?? r.OfflineId ?? "You're screwed", myr?.Id ?? other);
            }
            return map;
        }
        private IdMap MapProjectTypes(List<Projecttype> lst)
        {
            IdMap map = [];
            int other = dbContext.Projecttypes.FirstOrDefault(m => m.Name == "Generic")?.Id??0;

            foreach (Projecttype r in lst)
            {
                Projecttype? myr = dbContext.Projecttypes.FirstOrDefault(m => m.Name.ToLower() == r.Name.ToLower());
                map.TryAdd(r.StringId ?? r.OfflineId ?? "You're screwed", myr?.Id ?? other);
            }
            return map;
        }
        private IdMap MapRoles(List<Role> lst)
        {
            IdMap map = [];
            int def = dbContext.Roles.First(r => r.Rolename == RoleName.Admin && r.Orgrole).Id;

            foreach (Role r in lst)
            {
                Role? myr = dbContext.Roles.FirstOrDefault(m => m.Rolename == r.Rolename);
                map.TryAdd(r.StringId ?? r.OfflineId ?? "You're screwed", myr?.Id ?? def);
            }
            return map;
        }
        private IdMap MapWorkflowsteps(List<Workflowstep> lst)
        {
            IdMap map = [];
            int def = dbContext.Workflowsteps.FirstOrDefault(m => m.Name == "Done" && m.Process == "OBT")?.Id ?? 0;

            foreach (Workflowstep r in lst)
            {
                Workflowstep? myr = dbContext.Workflowsteps.FirstOrDefault(m => m.Name.ToLower() == r.Name.ToLower() && m.Process.ToLower() == r.Process.ToLower());
                map.TryAdd(r.StringId ?? r.OfflineId ?? "You're screwed", myr?.Id ?? def);
            }
            return map;
        }
        private IdMap MapIntegrations(List<Integration> lst)
        {
            IdMap map = [];
            foreach (Integration r in lst)
            {
                Integration? myr = dbContext.Integrations.FirstOrDefault(m => m.Name.ToLower() == r.Name.ToLower());
                if (myr != null)
                    map.TryAdd(r.StringId ?? r.OfflineId ?? "You're screwed", myr.Id);
            }
            return map;
        }
        private IdMap CopyArtifactCategorys(List<Artifactcategory> lst, int orgId)
        {
            Dictionary<string, Artifactcategory> map = [];
            foreach (Artifactcategory c in lst)
            {
                Artifactcategory? myc = dbContext.Artifactcategorys.FirstOrDefault(m => (m.OrganizationId == null || m.OrganizationId == orgId) && m.Categoryname == c.Categoryname && !m.Archived);
                string id = c.StringId ?? c.OfflineId ?? "error";
                if (myc == null)
                {
                    if (!map.ContainsKey(id))
                    {
                        EntityEntry<Artifactcategory>? t = dbContext.Artifactcategorys.Add(
                        new Artifactcategory
                        {
                            OrganizationId= orgId,
                            Categoryname = c.Categoryname,
                            Discussion = c.Discussion,
                            Resource = c.Resource,
                        });
                        map.Add(id, t.Entity);
                    }
                }
                else
                {
                    map.Add(id, myc);
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Artifactcategory> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private IdMap MapArtifactTypes(IList<ResourceObject> lst, string mapKey)
        {
            IdMap map = [];
            foreach (ResourceObject ro in lst)
            {
                Artifacttype a = ResourceObjectToResource(ro, new Artifacttype(), mapKey);

                Artifacttype? myc = dbContext.Artifacttypes.FirstOrDefault(m => m.OrganizationId == null && m.Typename == a.Typename && !m.Archived) ?? throw new Exception("missing type" + a.Typename);

                map.Add(a.OfflineId ?? "", myc.Id);
            }
            return map;
        }

        private IdMap CopyOrgworkflowsteps(IList<Orgworkflowstep> lst, int orgId)
        {
            Dictionary<string, Orgworkflowstep> map = [];
            foreach (Orgworkflowstep s in lst)
            {
                string id = s.StringId ?? s.OfflineId ?? "error";
                Orgworkflowstep? ex = dbContext.Orgworkflowsteps.Where(o => o.OrganizationId == orgId && o.Name == s.Name).FirstOrDefault();
                if (ex != null)
                    map.Add(id, ex);
                else
                {
                    if (!map.ContainsKey(id))
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
                        map.Add(id, t.Entity);
                    }
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Orgworkflowstep> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private IdMap FindOrgworkflowsteps(IList<Orgworkflowstep> lst, int orgId)
        {
            Dictionary<string, Orgworkflowstep> map = [];
            foreach (Orgworkflowstep s in lst)
            {
                string id = s.StringId ?? s.OfflineId ?? "error";
                Orgworkflowstep? ex = dbContext.Orgworkflowsteps.Where(o => o.OrganizationId == orgId && o.Name == s.Name).FirstOrDefault();
                if (ex != null)
                    map.Add(id, ex);
                else
                {
                    if (!map.ContainsKey(id))
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
                        map.Add(id, t.Entity);
                    }
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Orgworkflowstep> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;

        }
        private IdMap CopyOrgkeytermtargets(IList<Orgkeytermtarget> lst, int orgId)
        {
            Dictionary<string, Orgkeytermtarget> map = [];
            foreach (Orgkeytermtarget s in lst)
            {
                string id = s.StringId ?? s.OfflineId ?? "error";
                if (!map.ContainsKey(id))
                {
                    EntityEntry<Orgkeytermtarget>? t = dbContext.Orgkeytermtargets.Add(
                    new Orgkeytermtarget
                    {
                        OrganizationId= orgId,
                        Term = s.Term,
                        TermIndex = s.TermIndex,
                        Target = s.Target,
                        MediafileId = s.MediafileId, //mediafileMap.GetValueOrDefault(s.MediafileId)
                    });
                    map.Add(id, t.Entity);
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Orgkeytermtarget> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private void CopyIP(IList<Intellectualproperty> lst, int orgId)
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
                       ReleaseMediafileId = CheckValidId(ip.ReleaseMediafileId), // CheckValidId(ip.ReleaseMediafileId) == null ? null : MediafileMap.GetValueOrDefault(ip.ReleaseMediafileId),
                   });

            }
        }
        private IdMap CopySectionResources(IList<Sectionresource> lst, int orgId, int projectId)
        {
            Dictionary<string, Sectionresource> map = [];
            int internalize = dbContext.Orgworkflowsteps.ToList().Where(s => s.OrganizationId == orgId && s.Tool.Contains("{\"tool\": \"resource")).FirstOrDefault()?.Id ?? 0;
            foreach (Sectionresource sr in lst)
            {
                int stepId = sr.OrgWorkflowStepId; // owfsMap == null ? sr.OrgWorkflowStepId : owfsMap.GetValueOrDefault(sr.OrgWorkflowStepId);
                if (stepId == 0)
                    stepId = internalize;
                int? m = sr.MediafileId; //sr.MediafileId == null ? null : mediafileMap.GetValueOrDefault(sr.MediafileId);
                if (sr.MediafileId != null && (m ?? 0) == 0)
                {
                    Console.WriteLine($"Mediafile {sr.MediafileId} not found");
                }
                else
                {
                    string id = sr.StringId ?? sr.OfflineId ?? "error";
                    if (!map.ContainsKey(id))
                    {
                        EntityEntry<Sectionresource>? t =  dbContext.Sectionresources.Add(
                       new Sectionresource
                       {
                           SequenceNum = sr.SequenceNum,
                           Description = sr.Description,
                           SectionId = sr.SectionId, // sectionMap.GetValueOrDefault(sr.SectionId.ToString()),
                           MediafileId = m,
                           OrgWorkflowStepId = stepId,
                           PassageId = sr.PassageId, //sr.PassageId == null ? null : passageMap.GetValueOrDefault(sr.PassageId),
                           ProjectId = projectId
                       });
                        map.Add(id, t.Entity);
                    }
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Sectionresource> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private void CopySectionResourceUsers(IList<Sectionresourceuser> lst)
        {
            foreach (Sectionresourceuser sr in lst)
            {
                _ = dbContext.Sectionresourceusers.Add(
                   new Sectionresourceuser
                   {
                       SectionResourceId = sr.SectionResourceId, // sectionResourceMap.GetValueOrDefault(sr.SectionResourceId),
                       UserId = sr.UserId,
                   });
            }
        }
        private static string? MapStepComplete(string? source, IdMap? OrgworkflowstepMap)
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
                    if (int.TryParse(entry["stepid"]?.ToString(), out int id))
                        entry["stepid"] = OrgworkflowstepMap.GetValueOrDefault(id).ToString();
                }
                return result.ToString();
            }
            return null;
        }
        private async Task<IdMap> CopyMediafilesAsync(List<Mediafile> lst, bool sameOrg, Plan plan,
            int start, string mapKey, DateTime? dtBail, ZipArchive? archive)
        {
            IdMap map = GetMediafileMap(mapKey);

            string suffix = "_" + plan.Slug;
            for (int ix = start; ix < lst.Count && (dtBail == null || DateTime.Now < dtBail); ix++)
            {
                Mediafile m = lst[ix];
                string id = m.StringId ?? m.OfflineId ?? "error";
                if (!map.ContainsKey(id))
                {
                    //int? psgId = m.PassageId == null ? null : passageMap.GetValueOrDefault(m.PassageId);
                    if (m.PassageId == null && m.ArtifactTypeId == null)
                        throw new Exception("Passage not found " + m.PassageId);

                    Mediafile copym = new()
                    {
                        PassageId = m.PassageId,
                        VersionNumber = Convert.ToBoolean(m.VersionNumber) ? m.VersionNumber : 1,
                        ArtifactTypeId = CheckValidId(m.ArtifactTypeId), // == null ? null : artifacttypeMap?.GetValueOrDefault(m.ArtifactTypeId) ?? m.ArtifactTypeId,
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
                        ArtifactCategoryId = CheckValidId(m.ArtifactCategoryId), // CheckValidId(m.ArtifactCategoryId) == null ? null : sameOrg ? ValidArtifactCategory(m.ArtifactCategoryId) : artifactcategoryMap?.GetValueOrDefault(m.ArtifactCategoryId),
                        ResourcePassageId = CheckValidId(m.ResourcePassageId), //CheckValidId(m.ResourcePassageId) == null ? null : passageMap.GetValueOrDefault(m.ResourcePassageId) == 0 ? null : passageMap.GetValueOrDefault(m.ResourcePassageId),
                        RecordedbyUser = sameOrg ? m.RecordedbyUser : CurrentUser(),
                        OfflineId = "",
                        SourceMediaId = CheckValidId(m.SourceMediaId), //map.GetValueOrDefault(m.SourceMediaId??0)?.Id,
                        SourceSegments = m.SourceSegments,
                        SourceMediaOfflineId = "",
                        Transcriptionstate = m.Transcriptionstate,
                        Topic = m.Topic,
                    };
                    if (string.IsNullOrEmpty(copym.OriginalFile) && !string.IsNullOrEmpty(m.AudioUrl))
                    {
                        // Extract filename from AudioUrl
                        // Handle both cases: "media/filename.mp3" and "https://...../filename.mp3?params"
                        string audioUrl = m.AudioUrl;

                        // First, remove query string parameters if present
                        int queryIndex = audioUrl.IndexOf('?');
                        if (queryIndex > 0)
                        {
                            audioUrl = audioUrl[..queryIndex];
                        }

                        // Now extract the filename from the path
                        int lastSlashIndex = audioUrl.LastIndexOf('/');
                        if (lastSlashIndex >= 0 && lastSlashIndex < audioUrl.Length - 1)
                        {
                            copym.OriginalFile = audioUrl[(lastSlashIndex + 1)..];
                        }
                    }
                    copym.S3File = await mediaService.GetNewFileNameAsync(copym, suffix);
                    if ((m.S3File ?? "") == "" && archive is not null)
                    {
                        copym.AudioUrl = _S3Service
                            .SignedUrlForPut(
                                copym.S3File,
                                mediaService.DirectoryName(m),
                                m.ContentType ?? ""
                            )
                            .Message;
                        await CopyMediaFile(copym, archive);
                    }
                    else
                    {
                        await CopyMediafile(m, copym);
                    }

                    EntityEntry<Mediafile>? t =  dbContext.Mediafiles.Add(copym);
                    //we have to save after every one because we may have a link to previous mediafiles here
                    dbContext.SaveChanges();
                    map.Add(id, t.Entity.Id);
                    SaveId(Tables.Mediafiles, id, t.Entity.Id, mapKey);
                    dbContext.SaveChanges();
                }
            }
            return map;
        }
        private void CopyPassagestatechanges(IList<Passagestatechange> lst)
        {
            foreach (Passagestatechange p in lst)
            {
                EntityEntry<Passagestatechange>? t = dbContext.Passagestatechanges.Add(
                    new Passagestatechange
                    {
                        PassageId= p.PassageId, // passageMap.GetValueOrDefault(p.PassageId),
                        State = p.State,
                        Comments = p.Comments,
                    });
            }
        }
        private IdMap CopyDiscussions(IList<Discussion> lst)
        {
            Dictionary<string, Discussion> map = [];
            foreach (Discussion d in lst)
            {
                string id = d.StringId ?? d.OfflineId ?? "error";
                if (!map.ContainsKey(id))
                {
                    EntityEntry<Discussion>? t = dbContext.Discussions.Add(
                        new Discussion
                        {
                            ArtifactCategoryId = CheckValidId(d.ArtifactCategoryId), // == null ? null : sameOrg ? d.ArtifactCategoryId : acMap?.GetValueOrDefault(d.ArtifactCategoryId??0),
                            MediafileId = CheckValidId(d.MediafileId), // == null ? null : mediafileMap.GetValueOrDefault(d.MediafileId),
                            OrgWorkflowStepId = d.OrgWorkflowStepId, // orgwfMap == null ? d.OrgWorkflowStepId : orgwfMap.GetValueOrDefault(d.OrgWorkflowStepId),
                            GroupId = CheckValidId(d.GroupId), // sameOrg ? CheckValidId(d.GroupId) : null,
                            Resolved = d.Resolved,
                            Segments = d.Segments,
                            Subject = d.Subject,
                            UserId = d.User?.Id,
                            DateCreated = d.DateCreated,
                            DateUpdated = DateTime.UtcNow,
                        }
                    );
                    map.Add(id, t.Entity);
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Discussion> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private void CopyComments(IList<Comment> lst)
        {
            foreach (Comment c in lst)
            {
                int? mId = c.MediafileId; // CheckValidId(c.MediafileId) == null ? null : mediafileMap.GetValueOrDefault(c.MediafileId);
                if (mId != 0)
                {
                    _ = dbContext.Comments.Add(
                        new Comment
                        {
                            OfflineId = "",
                            OfflineMediafileId = "",
                            OfflineDiscussionId = "",
                            DiscussionId = c.DiscussionId, // discussionMap.GetValueOrDefault(c.DiscussionId),
                            CommentText = c.CommentText,
                            MediafileId = mId,
                            Visible = c.Visible,
                        }
                    );
                }
            }
        }

        private IdMap? GetArtifactCategoryMap(bool sameOrg, string newProjId)
        {
            if (sameOrg)
                return null;
            ArtifactCategoryMap ??= GetMap(Tables.ArtifactCategorys, newProjId);
            return ArtifactCategoryMap;
        }
        private IdMap? GetArtifactTypesMap(bool sameOrg, string newProjId)
        {
            if (sameOrg)
                return null;
            ArtifactTypesMap ??= GetMap(Tables.ArtifactTypes, newProjId);
            return ArtifactTypesMap;
        }
        private IdMap? GetOrgworkflowstepMap(bool sameOrg, string newProjId)
        {
            if (sameOrg)
                return null;
            OrgworkflowstepMap ??= GetMap(Tables.OrgWorkflowSteps, newProjId);
            return OrgworkflowstepMap;
        }
        private IdMap GetSectionMap(string newProjId)
        {
            SectionMap ??= GetMap(Tables.Sections, newProjId);
            return SectionMap;
        }
        private IdMap GetPassageMap(string newProjId)
        {
            PassageMap ??= GetMap(Tables.Passages, newProjId);
            return PassageMap;
        }
        private IdMap GetMediafileMap(string newProjId)
        {
            MediafileMap ??= GetMap(Tables.Mediafiles, newProjId);
            return MediafileMap;
        }
        private IdMap GetDiscussionMap(string newProjId)
        {
            DiscussionMap ??= GetMap(Tables.Discussions, newProjId);
            return DiscussionMap;
        }
        private IdMap GetSectionResourceMap(string newProjId)
        {
            SectionResourceMap ??= GetMap(Tables.SectionResources, newProjId);
            return SectionResourceMap;
        }
        private IdMap GetUserMap(string newProjId)
        {
            UserMap ??= GetMap(Tables.Users, newProjId) ?? [];
            return UserMap;
        }
        private void SaveId(string table, string oldId, int newId, string mapKey)
        {
            dbContext.Copyprojects.Add(new CopyProject()
            {
                Sourcetable = table,
                Newprojid = mapKey,
                Oldid = oldId,
                Newid = newId
            });
        }
        private void SaveMap(IdMap map, string table, string newProjId)
        {
            foreach (KeyValuePair<string, int> kvp in map)
            {
                SaveId(table, kvp.Key, kvp.Value, newProjId);
            }
            dbContext.SaveChanges();
        }
        private IdMap GetMap(string table, string newProjId)
        {
            IdMap map = [];
            IQueryable<CopyProject> cps = dbContext.Copyprojects.Where(c => c.Newprojid == newProjId && c.Sourcetable == table);
            foreach (CopyProject cp in cps)
            {
                map.TryAdd(cp.Oldid, cp.Newid);
            }
            return map;
        }
        public void RemoveCopyProject(string projId)
        {
            foreach (CopyProject cp in dbContext.Copyprojects.Where(c => c.Newprojid == projId))
                dbContext.Remove(cp);
            dbContext.SaveChanges();
        }
        private int GetSingleId(string table, string projId)
        {
            CopyProject? cp = dbContext.Copyprojects.Where(c => c.Newprojid == projId && c.Sourcetable == table).FirstOrDefault();
            return cp?.Newid ?? 0;
        }
        private int? GetMappedId(string table, string projId, string oldId)
        {
            if (projId == "")
                return null;
            if (!table.EndsWith('s'))
                table += "s";
            CopyProject? cp = dbContext.Copyprojects.Where(c => c.Newprojid == projId && c.Sourcetable == table.ToLower() && c.Oldid == oldId).FirstOrDefault();
            int id = 0;
            if (cp == null)
                int.TryParse(oldId, out id);
            return cp?.Newid ?? id;
        }
        private async Task<Fileresponse> ProcessImportCopyProjectAsync(
                Project sourceproject,
                bool sameOrg,
                int start,
                string? projId)
        {
            DateTime dtBail = DateTime.Now.AddSeconds(60);
            User currentuser = CurrentUser() ?? new User();
            IQueryable<Organization> sourceOrg = dbContext.Organizations.Where(o => o.Id == sourceproject.OrganizationId);
            Organization org = sourceOrg.FirstOrDefault() ?? new Organization();
            IQueryable<Plan> sourceplans = dbContext.Plans.Where(p=>p.ProjectId == sourceproject.Id && !p.Archived);
            Project project = new();
            Plan plan = new();
            string newProjId = projId??"";

            if (start == 0)
            {
                int oldOrg = org.Id;
                if (!sameOrg)
                    org = await CreateNewOrg(org, false, false, currentuser);
                project = CreateNewProject(sourceproject, false, org.Id, currentuser);
                plan = await CreateNewPlan(sourceplans.First(), project, currentuser);
                newProjId = project.Id.ToString();
                SaveId(Tables.Organizations, oldOrg.ToString(), org.Id, newProjId);
                SaveId(Tables.Plans, sourceplans.First().Id.ToString(), plan.Id, newProjId);
                await dbContext.SaveChangesAsync();
                start++;
            }
            else
            {
                if (!sameOrg)
                {
                    Organization? tmporg = dbContext.Organizations.Where(o => o.Id == GetSingleId("organizations", newProjId)).FirstOrDefault();
                    if (tmporg == null)
                        return ErrorResponse("Can't find new organization", sourceproject.Name);
                    org = tmporg;
                }
                Project? tmpProj = dbContext.Projects.Where(p => p.StringId == newProjId).FirstOrDefault();
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

                IQueryable<Orgkeytermtarget> oktt = dbContext.Orgkeytermtargets.Where(s => s.OrganizationId == sourceproject.OrganizationId);
                IQueryable<Artifactcategory> categories = dbContext.Artifactcategorys.Where(ac => ac.OrganizationId == null || ac.OrganizationId == sourceproject.OrganizationId);
                IQueryable<VWProject> sharednotes = dbContext.VWProjects.Where(x => x.ProjectId == project.Id && x.SharedResourceId != null);
                IQueryable<Note> supportingNotes = dbContext.Notes
                        .Join(sharednotes, n => n.ResourceId, sn => sn.SharedResourceId, (n, sn) => n);
                IQueryable<Intellectualproperty>? ip = OrgIPs(dbContext.Organizations.Where(o => o.Id == sourceproject.OrganizationId));
                IQueryable<Bible>  orgBibles = dbContext.BiblesData.Where(b => b.Id == -1); //don't copy bibles data
                IOrderedEnumerable<Mediafile> myMedia = ProjectMedia(oktt, categories, sectionresources,
                                                    ip, sourceplans, supportingNotes, orgBibles).OrderBy(m => m.Id);

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
                                ArtifactCategoryMap = CopyArtifactCategorys([.. categories], org.Id);
                                SaveMap(ArtifactCategoryMap, name, newProjId);
                            }
                            ix++;
                            break;

                        case Tables.IntellectualPropertys:
                            if (!sameOrg)
                            {
                                //copy but change the organization to current org
                                CopyIP([.. OrgIPs(sourceOrg)], org.Id);
                            }
                            ix++;
                            break;

                        case Tables.OrgWorkflowSteps:
                            if (!sameOrg)
                            {
                                OrgworkflowstepMap = CopyOrgworkflowsteps([.. dbContext.Orgworkflowsteps.Where(s => s.OrganizationId == sourceproject.OrganizationId)], org.Id);
                                SaveMap(OrgworkflowstepMap, name, newProjId);
                            }
                            ix++;
                            break;

                        case Tables.OrgKeyTermTargets:
                            if (!sameOrg)
                            {
                                CopyOrgkeytermtargets([.. dbContext.Orgkeytermtargets.Where(s => s.OrganizationId == sourceproject.OrganizationId)], org.Id);
                            }
                            ix++;
                            break;

                        case Tables.Sections:
                            SectionMap = CopySections([.. sourcesections], plan.Id);
                            SaveMap(SectionMap, name, newProjId);
                            ix++;
                            break;

                        case Tables.Passages:
                            //save these for sectionresources next
                            PassageMap = CopyPassages([.. sourcepassages]);
                            SaveMap(PassageMap, name, newProjId);
                            ix++;
                            break;

                        case Tables.SectionResources:
                            SectionResourceMap = CopySectionResources([.. sectionresources], org.Id, project.Id);
                            SaveMap(SectionResourceMap, name, newProjId);
                            ix++;
                            break;

                        case Tables.SectionResourceUsers:
                            if (sameOrg)
                            {
                                CopySectionResourceUsers([.. sectionresources
                                    .Join(
                                        dbContext.Sectionresourceusers,
                                        r => r.Id,
                                        u => u.SectionResourceId,
                                        (r, u) => u
                                    )
                                    .Where(x => !x.Archived)]);
                            }
                            ix++;
                            break;

                        case Tables.Mediafiles:
                            //Get any we did on a previous run
                            IdMap? prevmap = GetMediafileMap(newProjId);
                            IdMap map = await CopyMediafilesAsync([.. myMedia],  sameOrg, plan, prevmap.Count, newProjId, dtBail, null);
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
                            CopyPassagestatechanges([.. sourcepassages.Join(dbContext.Passagestatechanges,
                                                                        p => p.Id, psc => psc.PassageId,
                                                                        (p, psc) => psc
                                                                    )]);
                            ix++;
                            break;

                        case Tables.Discussions:
                            DiscussionMap = CopyDiscussions([.. PlanDiscussions(PlanMedia(sourceplans))]);
                            SaveMap(DiscussionMap, name, newProjId);
                            ix++;
                            break;

                        case Tables.Comments:
                            CopyComments([.. dbContext.Comments
                            .Join(PlanDiscussions(PlanMedia(sourceplans)), c => c.DiscussionId, d => d.Id, (c, d) => c)
                            .Where(x => !x.Archived)]);
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
                    Message = string.Format("{0} {1} {2} {3}", org.Id, org.Name, project.Name, complete ? "" : status),
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
        public async Task<Fileresponse> ProcessImportCopyFileIntoOrgAsync(
                ZipArchive archive,
                int existingOrgId,
                string sFile,
                int start,
                string? myMapKey)
        {
            //can't wait for a new project id since we have to process 20 entries before then
            //use
            //give myself 20 seconds to get as much as I can...
            DateTime? dtBail = null; //HEY PUT THIS BACK! DateTime.Now.AddSeconds(20);
            User currentuser = CurrentUser() ?? new User();
            try
            {
                HttpContext?.SetFP("copy");

                Project? sourceproject = ReadFileProject(archive, myMapKey ?? "");

                string mapKey = myMapKey ?? $"{sourceproject?.OfflineId}{DateTime.Now.Ticks}";

                bool newOrg = (existingOrgId == 0);
                bool sameOrg = !newOrg && sourceproject?.Id > 0 && sourceproject?.OrganizationId == existingOrgId;

                if (start == 0)
                {
                    DateTime? sourceDate = CheckSILTranscriber(archive);
                    if (sourceDate == null)
                        return ErrorResponse("SILTranscriber not present", sFile);
                    Organization? fileorg = ReadFileOrganization(archive, mapKey);
                    if (existingOrgId == 0 && fileorg == null)
                        return ErrorResponse("No organization found in file", sFile);
                    if (sourceproject == null)
                        return ErrorResponse("No project found in file", sFile);
                }
                /*Project? project = newProjId != "" ? dbContext.Projects.FirstOrDefault(o => o.StringId == newProjId) : null;
int existingOrgId = project?.OrganizationId ?? orgId;
Organization? org = existingOrgId > 0 ? dbContext.Organizations.FirstOrDefault(o => o.Id == existingOrgId) : null;
Plan? plan = newProjId > 0 ? dbContext.Plans.FirstOrDefault(p => p.Id == GetSingleId(Tables.Plans, newProjId)) : null;
                                    org ??= await CreateNewOrg(fileorg, false, currentuser);
                    project ??= CreateNewProject(sourceproject, false, org.Id, currentuser);

                    newProjId = project?.Id ?? 0;
                    SaveId(Tables.Organizations, fileorg.Id.ToString(), org.Id, newProjId);
                    await dbContext.SaveChangesAsync();

                if (project == null || newProjId == 0)
                    return ErrorResponse("Can't find project", sFile);
                if (org == null)
                    return ErrorResponse("Can't find organization", sFile);
                */


                IJsonApiOptions options = new JsonApiOptions();
                bool complete = true;
                int entryNum = 0;
                int orgid = existingOrgId > 0 ? existingOrgId : mapKey != "" ? GetSingleId(Tables.Organizations, mapKey) : 0;
                Project? project = null;
                Plan? plan = null;
                string status = "";
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (DateTime.Now >= dtBail)
                    {
                        complete = false;
                        break;
                    }
                    if (!entry.FullName.StartsWith("data"))
                        continue;
                    if (entryNum++ < start)
                    {
                        continue;
                    }
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
                    Logger.LogInformation("name: {n}", name);
#pragma warning disable CS8604 // Possible null reference argument.
                    switch (name)
                    {
                        case Tables.Users:
                            List<User> users = [];
                            foreach (ResourceObject ro in lst)
                                users.Add(ResourceObjectToResource(ro, new User(), mapKey));
                            UserMap = MapUsers(users);
                            SaveMap(UserMap, name, mapKey);
                            break;
                        case Tables.ActivityStates:
                            List<Activitystate> acs = [];
                            foreach (ResourceObject ro in lst)
                                acs.Add(ResourceObjectToResource(ro, new Activitystate(), mapKey));
                            ActivityStateMap = MapActivityStates(acs);
                            SaveMap(ActivityStateMap, name, mapKey);
                            break;
                        case Tables.Integrations:
                            List<Integration> records = [];
                            foreach (ResourceObject ro in lst)
                                records.Add(ResourceObjectToResource(ro, new Integration(), mapKey));
                            IntegrationMap = MapIntegrations(records);
                            SaveMap(IntegrationMap, name, mapKey);
                            break;
                        case Tables.Organizations:
                            Organization org = ResourceObjectToResource(lst[0], new Organization(), mapKey);
                            if (newOrg)
                            {
                                Organization neworg = await CreateNewOrg(org, true, true, currentuser);
                                SaveId(name, org.OfflineId, neworg.Id, mapKey);
                                orgid = neworg.Id;
                            }
                            else
                                SaveId(name, org.OfflineId, existingOrgId, mapKey);
                            //add all users to the org
                            AddUsersToOrg(orgid, mapKey);
                            InviteUserToOrg(orgid, "greg_trihus+1@sil.org");
                            InviteUserToOrg(orgid, "nathan_payne+1@sil.org");
                            InviteUserToOrg(orgid, "maggiecampo25+1@gmail.com");
                            break;
                        case Tables.PassageTypes:
                            List<Passagetype> pts = [];
                            foreach (ResourceObject ro in lst)
                                pts.Add(ResourceObjectToResource(ro, new Passagetype(), mapKey));
                            PassageTypeMap = MapPassageTypes(pts);
                            SaveMap(PassageTypeMap, name, mapKey);
                            break;
                        case Tables.PlanTypes:
                            List <Plantype> plts = [];
                            foreach (ResourceObject ro in lst)
                                plts.Add(ResourceObjectToResource(ro, new Plantype(), mapKey));
                            PlanTypeMap = MapPlanTypes(plts);
                            SaveMap(PlanTypeMap, name, mapKey);
                            break;
                        case Tables.ProjectTypes:
                            List <Projecttype> prts = [];
                            foreach (ResourceObject ro in lst)
                                prts.Add(ResourceObjectToResource(ro, new Projecttype(), mapKey));
                            ProjectTypeMap = MapProjectTypes(prts);
                            SaveMap(ProjectTypeMap, name, mapKey);
                            break;
                        case Tables.Roles:
                            List <Role> roles = [];
                            foreach (ResourceObject ro in lst)
                                roles.Add(ResourceObjectToResource(ro, new Role(), mapKey));
                            RoleMap = MapRoles(roles);
                            SaveMap(ProjectTypeMap, name, mapKey);
                            break;
                        case Tables.WorkflowSteps:
                            List <Workflowstep> workflowsteps = [];
                            foreach (ResourceObject ro in lst)
                                workflowsteps.Add(ResourceObjectToResource(ro, new Workflowstep(), mapKey));
                            WorkflowStepMap = MapWorkflowsteps(workflowsteps);
                            SaveMap(WorkflowStepMap, name, mapKey);
                            break;
                        case Tables.ArtifactCategorys:
                            List<Artifactcategory> ac = [];
                            if (orgid == 0)
                                throw new Exception("No Org in ArtifactCategory");
                            foreach (ResourceObject ro in lst)
                                ac.Add(ResourceObjectToResource(ro, new Artifactcategory(), mapKey));
                            ArtifactCategoryMap = CopyArtifactCategorys([.. ac.Where(s => s.OrganizationId == orgid || s.OrganizationId is null)], orgid);
                            SaveMap(ArtifactCategoryMap, name, mapKey);
                            break;

                        case Tables.ArtifactTypes:
                            ArtifactTypesMap = MapArtifactTypes(lst, mapKey);
                            SaveMap(ArtifactTypesMap, name, mapKey);
                            break;

                        case Tables.Groups:
                            if (orgid == 0)
                                throw new Exception("No Org in Groups");
                            //get it from the org
                            int grpId = dbContext.Groups.FirstOrDefault(g => g.OwnerId == orgid)?.Id ?? 0;
                            if (grpId > 0)
                            {
                                Group grp = ResourceObjectToResource(lst[0], new Group(), mapKey);
                                SaveId(name, grp.OfflineId, grpId, mapKey);
                            }
                            AddUsersToGroup(grpId, mapKey);
                            break;
                        case Tables.OrganizationMemberships:
                            //ignore - we already added all our users to the org
                            continue;

                        case Tables.OrgWorkflowSteps:
                            List<Orgworkflowstep> owlst = [];
                            foreach (ResourceObject ro in lst)
                                owlst.Add(ResourceObjectToResource(ro, new Orgworkflowstep(), mapKey));
                            OrgworkflowstepMap = CopyOrgworkflowsteps(owlst, orgid);
                            SaveMap(OrgworkflowstepMap, name, mapKey);
                            break;

                        case Tables.GroupMemberships:
                            //ignore - we already added all our users to the group
                            continue;

                        case Tables.Projects:
                            Project p = ResourceObjectToResource(lst.First(), new Project(), mapKey);
                            project = CreateNewProject(p, true, orgid, currentuser);
                            SaveId(Tables.Projects, p.OfflineId, project.Id, mapKey);
                            break;

                        /*
                    case Tables.IntellectualPropertys:
                                if (orgId > 0) //we don't need a map so skip if we aren't a new org
                                    continue;
                                //copy but change the organization to current org
                                List<Intellectualproperty> iplst = [];
                                foreach (ResourceObject ro in lst)
                                    iplst.Add(ResourceObjectToResource(ro, new Intellectualproperty()));
                                CopyIP(iplst, org.Id, GetMediafileMap(mapKey));
                                break;
*/

                        case Tables.Plans:
                            if (project is null)
                            {
                                int id = GetSingleId(Tables.Projects, mapKey);
                                project = dbContext.Projects.Find(id);
                            }
                            Plan sourceplan = ResourceObjectToResource(lst.First(), new Plan(), mapKey);
                            sourceplan.Flat = false; //force hierarchical so it can be published;
                            plan = await CreateNewPlan(sourceplan, project, currentuser);
                            SaveId(Tables.Plans, sourceplan.OfflineId, plan.Id, mapKey);
                            break;

                        case Tables.Sections:
                            if (plan is null)
                            {
                                int id = GetSingleId(Tables.Plans, mapKey);
                                plan = dbContext.Plans.Find(id);
                            }
                            List<Section> slst = [];
                            foreach (ResourceObject ro in lst)
                                slst.Add(ResourceObjectToResource(ro, new Section(), mapKey));

                            SectionMap = CopySections(slst, plan?.Id ?? 0);
                            SaveMap(SectionMap, name, mapKey);
                            break;

                        case Tables.Passages:
                            List<Passage> plst = [];
                            foreach (ResourceObject ro in lst)
                                plst.Add(ResourceObjectToResource(ro, new Passage(), mapKey));
                            PassageMap = CopyPassages(plst);
                            SaveMap(PassageMap, name, mapKey);
                            break;

                        case Tables.SectionResources:
                            List<Sectionresource> srlst = [];
                            foreach (ResourceObject ro in lst)
                                srlst.Add(ResourceObjectToResource(ro, new Sectionresource(), mapKey));
                            SectionResourceMap = CopySectionResources(srlst, orgid, GetSingleId(Tables.Projects, mapKey));
                            SaveMap(SectionResourceMap, name, mapKey);
                            break;

                        case Tables.SectionResourceUsers:
                            if (!sameOrg) //don't store user info for a new org
                                break;
                            List<Sectionresourceuser> ulst = [];
                            foreach (ResourceObject ro in lst)
                                ulst.Add(ResourceObjectToResource(ro, new Sectionresourceuser(), mapKey));
                            CopySectionResourceUsers(ulst);
                            break;

                        case Tables.Mediafiles:
                            List<Mediafile> mflst = [];
                            foreach (ResourceObject ro in lst)
                                mflst.Add(ResourceObjectToResource(ro, new Mediafile(), mapKey));
                            IdMap? prevmap = GetMediafileMap(mapKey);
                            if (plan is null)
                            {
                                int id = GetSingleId(Tables.Plans, mapKey);
                                plan = dbContext.Plans.Find(id);
                            }
                            IdMap map = await CopyMediafilesAsync(mflst, sameOrg, plan, prevmap.Count, mapKey, dtBail,archive);
                            if (map.Count == mflst.Count)
                            {
                                MediafileMap = null;
                                //reset MediafileMap with all
                                GetMediafileMap(mapKey);
                                status = "";
                            }
                            else
                            {
                                status = $"{map.Count}/{mflst.Count} mediafiles copied";
                                complete = false;
                                entryNum--; //we must have bailed out because of time, so continue to start here.
                            }
                            break;

                        case Tables.Discussions:
                            List<Discussion> dlst = [];
                            foreach (ResourceObject ro in lst)
                                dlst.Add(ResourceObjectToResource(ro, new Discussion(), mapKey));
                            DiscussionMap = CopyDiscussions(dlst);
                            SaveMap(DiscussionMap, name, mapKey);
                            break;

                        case Tables.Comments:
                            List<Comment> clst = [];
                            foreach (ResourceObject ro in lst)
                                clst.Add(ResourceObjectToResource(ro, new Comment(), mapKey));
                            CopyComments(clst);
                            break;

                    }
                }

                _ = dbContext.SaveChanges();
#pragma warning restore CS8604 // Possible null reference argument.

                return new Fileresponse()
                {
                    Id = complete ? -1 : entryNum,
                    //Message = string.Format("{0} {1} {2} {3}", org.Id, org.Name, project.Name, complete ? "" : "Some status here"),
                    Message = status,
                    FileURL = mapKey,
                    Status = complete ? HttpStatusCode.OK : HttpStatusCode.PartialContent,
                    Startindex = complete ? "" : entryNum.ToString(),
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
        private async Task<Fileresponse> ProcessImportCopyFileAsync(
                ZipArchive archive,
                bool neworg,
                string sFile)
        {

            DateTime? sourceDate = CheckSILTranscriber(archive);
            if (sourceDate == null)
                return ErrorResponse("SILTranscriber not present", sFile);

            int orgId = 0;

            //check project
            Project? sourceproject = ReadFileProject(archive, "NoMapKey");
            bool sameOrg = !neworg && sourceproject != null;
            if (sameOrg)
            {
                int orgid = sourceproject?.OrganizationId??0;
                Organization? org = dbContext.Organizations.FirstOrDefault(o => o.Id == orgid);
                if (org == null && !neworg)
                {
                    Organization? fileorg = ReadFileOrganization(archive, "NoMapKey");
                    User currentuser = CurrentUser() ?? new User();
                    string orgName = fileorg?.Name ?? "Unknown";
                    org = dbContext.Organizations.FirstOrDefault(o => o.Name == orgName && o.OwnerId == currentuser.Id && !o.Archived);
                }
                orgId = org?.Id ?? 0;
            }
            //TODO add start and newProjId parameters to allow resuming
            return await ProcessImportCopyFileIntoOrgAsync(archive, orgId, sFile, 0, null);
        }
    }
    #endregion Copy
}
