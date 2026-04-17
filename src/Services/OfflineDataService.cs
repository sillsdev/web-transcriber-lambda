using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Serialization.Response;
using Microsoft.EntityFrameworkCore;
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
        private const int MediafileChunkSize = 50;
        private const int DataChunkSize = 100;


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
            {Tables.Graphics, 'I' },
            {Tables.Comments,'J'},
            {Tables.SectionResourceUsers,'J'},
            {Tables.SharedResourceReferences, 'J' }

        };
        IdMap? MediafileMap = null;
        IdMap? UserMap = null;
        private readonly List<string> UsersToInvite = [];

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
            int member = dbContext.Roles.Where(r => r.Rolename == RoleName.Member).FirstOrDefault()?.Id ?? 5;
            foreach (int u in distinctValues)
            {
                if (dbContext.Organizationmemberships.FirstOrDefault(om => om.UserId == u && om.OrganizationId == orgid) == null)
                {
                    Organizationmembership om = new ()
                    {
                        UserId = u,
                        OrganizationId = orgid,
                        RoleId = member
                    };

                    dbContext.Organizationmemberships.Add(om);
                }
            }
            dbContext.SaveChanges();
        }
        private void InviteUserToOrg(int orgid, string email)
        {
            if (!dbContext.Invitations.Where(i => i.OrganizationId == orgid && i.Email == email).Any())
            {
                int member = dbContext.Roles.Where(r => r.Rolename == RoleName.Member).FirstOrDefault()?.Id ?? 5;
                Invitation i = new ()
                {
                    RoleId = member,
                    OrganizationId = orgid,
                    Email = email,
                    AllUsersRoleId = member,
                    LoginLink = "https://app-dev.audioprojectmanager.org", //doesn't matter because we aren't actually sending the email
                    InvitedBy=CurrentUser()?.Email ?? "sara_hentzel@sil.org"
                };
                dbContext.Invitations.Add(i);
            }
        }
        private void AddUsersToGroup(int grpid, string mapKey)
        {
            IdMap users = GetUserMap(mapKey);
            //get the distinct values and add them to the orgmems
            IEnumerable<int> distinctValues = users.Values.Distinct();
            int member = dbContext.Roles.Where(r => r.Rolename == RoleName.Member).FirstOrDefault()?.Id ?? 5;
            foreach (int u in distinctValues)
            {
                if (dbContext.Groupmemberships.FirstOrDefault(om => om.UserId == u && om.GroupId == grpid) == null)
                {
                    Groupmembership om = new ()
                    {
                        UserId = u,
                        GroupId = grpid,
                        RoleId = member,
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
            bool validDataRead = false;
            try
            {
                Stream ms = OpenFile(fileName + ".sss", out DateTime writeTime);
                StreamReader reader = new(ms);
                string data = reader.ReadToEnd();
                bool recent = writeTime > DateTime.Now.AddMinutes(data.Contains("writing") ? -8 : -4);

                Logger.LogInformation("CheckProgress: fileName={fn}, lastAdd={la}, writeTime={wt}, recent={r}, dataLength={dl}",
                    fileName, lastAdd, writeTime, recent, data.Length);

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
                    if (int.TryParse(data, out startNext))
                    {
                        validDataRead = true;
                        if (media)
                            startNext += lastAdd;
                        Logger.LogInformation("CheckProgress: Parsed startNext={sn} from status file", startNext);
                    }
                    else
                    {
                        Logger.LogWarning("CheckProgress: Failed to parse data='{d}'", data);
                        startNext = 0;
                    }
                }
                else
                {
                    Logger.LogWarning("CheckProgress: Status file not recent, writeTime={wt}", writeTime);
                    startNext = 0;
                }
            }
            catch (Exception ex)
            {
                //it's not there yet...
                Logger.LogWarning(ex, "CheckProgress: {sf} status file not available", fileName + ".sss");
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
            else if (!validDataRead || startNext == 0)
            {
                // Only enforce lastAdd + 1 when we didn't get valid data or when the file is stale
                startNext = lastAdd + 1;
            }
            else
            {
                // We have valid data - use it, but ensure we're making progress
                startNext = Math.Max(startNext, lastAdd + 1);
            }

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
                        .Where(x => !x.Archived).OrderBy(r => r.Id);
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
            List<Mediafile> pm = [.. PlanMedia(plans)];
            List<Mediafile> myMedia = [.. pm
                                .Concat([.. okttmedia])
                                .Concat([.. ipmedia])
                                .Concat([.. sourcemediafiles])
                                .Concat([.. categorymediafiles])
                                .Concat([.. sharedNoteMedia])
                                .Concat([.. bibleMedia])
                                .Concat([.. bibleisoMedia])
                                .Distinct().OrderBy(m => m.Id)];

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
                    List<int> srIds = [..sharednotes.Select(n => n.SharedResourceId??0).Distinct()];
                    srIds.AddRange([.. supportingNotes.Select(n => n.ResourceId ?? 0).Distinct()]);

                    IQueryable<Sharedresource>? sharedresources = dbContext.SharedresourcesData
                                    .Where(a => !a.Archived && srIds.Contains(a.Id));
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

        private static bool IsDirectSyncArchive(ZipArchive archive)
        {
            return archive.Entries.Any(e =>
                e.FullName.Equals("SILTranscriber", StringComparison.OrdinalIgnoreCase)
                || e.FullName.Equals("Version", StringComparison.OrdinalIgnoreCase)
                || e.FullName.StartsWith("data/", StringComparison.OrdinalIgnoreCase));
        }

        public async Task<Fileresponse> ImportSyncFileAsync(string sFile, int fileIndex, int start)
        {
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            S3Response response = await _S3Service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return FileNotFound(sFile);
            ZipArchive archive = new(response.FileStream);

            if (IsDirectSyncArchive(archive))
            {
                Fileresponse fr = await ProcessSyncFileAsync(archive, 0, sFile, start, dtBail);
                fr.Startindex = string.Format("0/{0}", fr.Startindex);
                return fr;
            }

            List<string> report = [];
            List<string> errors = [];
            string startIndex = "0/0";
            for (int ix = fileIndex; ix < archive.Entries.Count; ix++)
            {
                ZipArchiveEntry entry = archive.Entries[ix];
                ZipArchive zipEntry = new(entry.Open());
                Fileresponse fr = await ProcessSyncFileAsync(zipEntry, 0, entry.Name, start, dtBail);
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
                    startIndex = string.Format("{0}/{1}", ix, fr.Startindex);
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

        public async Task<Fileresponse> ImportSyncFileAsync(int projectId, string sFile, int start)
        {
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            S3Response response = await _S3Service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return FileNotFound(sFile);
            ZipArchive archive = new(response.FileStream);
            return await ProcessSyncFileAsync(archive, projectId, sFile, start, dtBail);
        }
        //DEPRECATED!!
        public async Task<Fileresponse> ImportCopyFileAsyncDeprecated(bool neworg, string sFile)
        {
            S3Response response = await _S3Service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return FileNotFound(sFile);
            ZipArchive archive = new(response.FileStream);
            return await ProcessImportDeprecatedCopyFileAsync(archive, neworg, sFile);
        }
        public async Task<Fileresponse> ImportCopyProjectAsyncDeprecated(bool neworg, int projectId, int start, string? newProjId)
        {
            Project? sourceproject = dbContext.Projects.FirstOrDefault(p => p.Id==projectId);
            return sourceproject == null
                ? ErrorResponse("Project not found", projectId.ToString())
                : await ProcessImportCopyProjectDeprecatedAsync(sourceproject, neworg, start, newProjId);
        }
        public async Task<Fileresponse> ImportCopyProjectAsync(int orgid, int projectId, int start, string? mapKey)
        {
            Project? sourceproject = dbContext.Projects.FirstOrDefault(p => p.Id==projectId);
            return sourceproject == null
                ? ErrorResponse("Project not found", projectId.ToString())
                : await ProcessImportCopyProjectAsync(sourceproject, orgid, start, mapKey);
        }
        public async Task<Fileresponse> ImportCopyFileIntoOrgAsync(int org, string sFile, int start, string? mapKey)
        {
            S3Response response = await _S3Service.ReadObjectDataAsync(sFile, "imports");
            if (response.FileStream == null)
                return FileNotFound(sFile);
            ZipArchive archive = new(response.FileStream);
            return await ProcessImportCopyFileAsync(archive, org, sFile, start, mapKey);

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
            string ext = Path.GetExtension(sFile);
            string contentType = ext.Length > 1 ? string.Concat("application/", ext[1..]) : "application/json";
            return new Fileresponse()
            {
                Message = msg,
                FileURL = sFile,
                Status = status,
                ContentType = contentType,
                Startindex = ""
            };
        }

        private async Task<bool> CopyMediafile(string? originalS3File, int originalPlan, Mediafile? target)
        {
            if (originalS3File != null && target?.S3File != null)
            {
                string folder = mediaService.DirectoryName(originalPlan);
                S3Response response = await _S3Service.CopyFile(originalS3File, target.S3File, folder, mediaService.DirectoryName(target));
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

        private async Task CopyMediaFile(string originalS3File, Mediafile m, ZipArchive archive)
        {
            ZipArchiveEntry? f = archive.Entries
                .Where(e => e.Name == originalS3File)
                .FirstOrDefault();
            f ??= archive.Entries
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
                    .Where(m => m.OfflineId == c.OfflineMediafileId && !m.Archived)
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
                    .Where(m => m.OfflineId == c.OfflineDiscussionId && !m.Archived)
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
                    .Where(m => m.OfflineId == d.OfflineMediafileId && !m.Archived)
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
                c => c.SourceMediaId == null && c.OfflineSourceMediaId != null
            )];
            foreach (Mediafile m in mediafiles)
            {
                Mediafile? sourcemedia = dbContext.Mediafiles
                    .Where(sm => sm.OfflineId == m.OfflineSourceMediaId && !m.Archived)
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
                    .Where(m => m.OfflineId == c.OfflineMediafileId && ! m.Archived)
                    .FirstOrDefault();
                if (mediafile != null)
                {
                    c.OfflineMediafileId = null;
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
                    .Where(m => m.OfflineId == c.OfflineMediafileId && ! m.Archived)
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
                existing.OfflineDiscussionId = importing.OfflineDiscussionId;
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
                    || existing.OfflineSourceMediaId != (importing.OfflineSourceMediaId ?? importing.SourceMediaOfflineId)
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
                existing.OfflineSourceMediaId = importing.OfflineSourceMediaId ?? importing.SourceMediaOfflineId;
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
        private string GetRelationshipId<TResource>(ResourceObject ro, string relationship)
            where TResource : class, IIdentifiable
        {
            ResourceType resourceType = _resourceGraph.GetResourceType(typeof(TResource));
            IReadOnlyCollection<RelationshipAttribute>? rels = resourceType.Relationships;
            RelationshipAttribute? myTypeRelationship = rels.FirstOrDefault(
                        r => r.PublicName == relationship
                    );
            if (ro.Relationships == null)
                return "";
            KeyValuePair<string, RelationshipObject?> row = ro.Relationships.Where(
                        rel => rel.Key == relationship
                    ).FirstOrDefault();
            return row.Value?.Data.SingleValue?.Id ?? "";
        }
        private TResource ResourceObjectToResource<TResource>(ResourceObject ro, TResource s, string mapKey = "")
            where TResource : class, IIdentifiable
        {
            ResourceType resourceType = _resourceGraph.GetResourceType(typeof(TResource));
            IReadOnlyCollection<AttrAttribute>? attrs = resourceType.Attributes;
            IReadOnlyCollection<RelationshipAttribute>? rels = resourceType.Relationships;

            if (mapKey == "" && IsNumber(ro.Id))
                s.StringId = ro.Id;

            if (ro.Attributes != null)
                foreach (KeyValuePair<string, object?> row in ro.Attributes)
                {
                    AttrAttribute? myTypeAttribute = attrs.FirstOrDefault(
                        a => a.PublicName == row.Key
                    );
                    myTypeAttribute ??= attrs.FirstOrDefault(a => a.PublicName == row.Key.CamelToKebab());

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
            if (ro.Id != null)
            {
                AttrAttribute? offlineIdAttribute = attrs.FirstOrDefault(
                        a => a.PublicName == "offline-id");
                offlineIdAttribute?.SetValue(s, ro.Id);
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
                    myTypeRelationship ??= rels.FirstOrDefault(
                        r => r.PublicName == row.Key.CamelToKebab()
                    );
                    if (myTypeRelationship != null)
                    {
                        string oldIdStr = row.Value?.Data.SingleValue?.Id??"";

                        bool isNum = int.TryParse(oldIdStr, out int oldid);
                        int id = !string.IsNullOrEmpty(mapKey) && !string.IsNullOrEmpty(oldIdStr)
                            ? GetMappedId(myTypeRelationship.Property.PropertyType.Name, mapKey, oldIdStr) ?? 0
                            : oldid;

                        AttrAttribute? offlineAttribute = attrs.FirstOrDefault(
                            a => a.PublicName == "offline-" + myTypeRelationship.PublicName + "-id");
                        if (offlineAttribute != null && mapKey != "")
                            offlineAttribute?.SetValue(s, oldIdStr);
                        AttrAttribute? myIdAttribute = attrs.FirstOrDefault(
                        a => a.PublicName == myTypeRelationship.PublicName + "-id");
                        if (myIdAttribute == null && myTypeRelationship.PublicName == "last-modified-by-user")
                            myIdAttribute = attrs.FirstOrDefault(a => a.PublicName == "last-modified-by");
                        try
                        {
                            object? p = null;
                            if (id > 0)
                                p = dbContext.Find(myTypeRelationship.Property.PropertyType, id);

                            if (p != null)
                                myTypeRelationship.SetValue(s, p);
                            else
                                id = 0;
                        }
                        catch (Exception e)
                        {
                            Logger.LogError("unable to find {r} with id {id} oldid {oldid} {e}", myTypeRelationship.PublicName, id, oldIdStr, e);
                            id = 0;
                        }
                        if (myIdAttribute != null)
                            if (id > 0)
                                myIdAttribute.SetValue(s, id);
                            else
                                myIdAttribute.SetValue(s, null);

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
            if (fileproject == null)
                return null;
            //is it from our db?
            if (fileproject.Id > 0)
            {
                Project? project = dbContext.Projects
                .FirstOrDefault(p => p.Id == fileproject.Id);
                //verify the other fields are the same
                if (project == null ||
                    project.Name != fileproject.Name ||
                    project.OrganizationId != fileproject.OrganizationId)
                    fileproject.Id = 0; //treat as new
            }
            return fileproject;
        }
        private Organization? ReadFileOrganization(ZipArchive archive)
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

            return fileorg == null ? null : ResourceObjectToResource(fileorg, new Organization());
        }

        private int UpdateUsers(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId) //at least do one
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
                    user.HotKeys = u.HotKeys;
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
                if (DateTime.Now > dtBail && lastIndex > startId)
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
                if (DateTime.Now > dtBail && lastIndex > startId)
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
                if (DateTime.Now > dtBail && lastIndex > startId)
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
                                            .Where(x => x.OfflineId == d.OfflineId && !x.Archived)
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
        private int CreateOrUpdateComments(IList<ResourceObject> lst, int startId, DateTime sourceDate, List<string> report, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
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
                        .Where(x => x.OfflineId == c.OfflineId && !x.Archived)
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
                    mediafile = dbContext.Mediafiles.FirstOrDefault(x => x.OfflineId == m.OfflineId && !x.Archived);
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
                        string originalS3 = m.S3File??"";
                        m.S3File = await mediaService.GetNewFileNameAsync(m);
                        await CopyMediaFile(originalS3, m, archive);
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
                                OfflineSourceMediaId = m.OfflineSourceMediaId ?? m.SourceMediaOfflineId,
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
                if (DateTime.Now > dtBail && lastIndex > startId)
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
                if (DateTime.Now > dtBail && lastIndex > startId)
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
                if (DateTime.Now > dtBail && lastIndex > startId)
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
                                        .Where(x => x.OfflineId == ip.OfflineId && !x.Archived)
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
        private int CreateOrgKeyTermTargets(IList<ResourceObject> lst, int startId, DateTime dtBail)
        {
            for (int lastIndex = startId; lastIndex < lst.Count; lastIndex++)
            {
                if (DateTime.Now > dtBail && lastIndex > startId)
                    return lastIndex;
                ResourceObject ro = lst[lastIndex];
                Orgkeytermtarget tt = ResourceObjectToResource(ro, new Orgkeytermtarget(), "");
                if (tt.Id == 0)
                {
                    //check if it's been uploaded another way (ie. itf and now we're itfs or vice versa)
                    Orgkeytermtarget? ktt = dbContext.Orgkeytermtargets
                                        .Where(x => x.OfflineId == tt.OfflineId && !x.Archived)
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

        private async Task<Fileresponse> ProcessSyncFileAsync(
            ZipArchive archive,
            int projectid,
            string sFile,
            int start,
            DateTime dtBail
        )
        {
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
                while (start < archive.Entries.Count && DateTime.Now < dtBail)
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
                            List<Mediafile> sorted = [];
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
                            startId = CreateIPs(lst, startId, dtBail);
                            break;

                        case Tables.OrgKeyTermTargets:
                            startId = CreateOrgKeyTermTargets(lst, startId, dtBail);
                            break;

                        default:
                            startId = -1;
                            break;

                    }
                    start = StartIndex.SetStart(start, ref startId);
                }
                int ret = await dbContext.SaveChangesNoTimestampAsync();
                if (start == archive.Entries.Count)
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
                try
                {
                    JObject? x = JObject.Parse(sourceOrg.DefaultParams);
                    JToken? lpToken = x?["langProps"];
                    if (lpToken is JObject lp)
                    {
                        lang = lp["languageName"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(lang))
                            lang = " " + lang;
                    }
                }
                catch
                {
                    // If JSON parsing fails or structure is unexpected, just use empty lang
                }
            }
            string sourceName = (sourceOrg.Name??"team").Replace(">", "").Replace("<", "");
            string orgname = sourceName+lang+(sameName ? "" : "_c"+tryn++).ToString();
            while (dbContext.Organizations.FirstOrDefault(x => x.Name == orgname && !x.Archived) != null)
            {
                orgname = sourceName + lang + "_c" + tryn++.ToString();
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
            while (dbContext.Projects.FirstOrDefault(x => x.OrganizationId == orgId && x.Name == projname && !x.Archived) != null)
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
                    AllowClaim = source.AllowClaim,
                    // Publishing/Editing permissions 
                    EditsheetGroupId = null,
                    EditsheetUserId = null,
                    PublishGroupId = null,
                    PublishUserId = null,
                    // Slug and DateArchived will be handled automatically or should be null for new projects
                });
            dbContext.SaveChanges();

            return t.Entity;
        }
        private async Task<Plan> CreateNewPlan(Plan source, Project project, User user)
        {
            EntityEntry<Plan>? t = dbContext.Plans.Add(
                new Plan
                {
                    Name = project.Name, //use the new project name which might have been modified so as not to duplicate source project name if in same org
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
        private IdMap CopySections(List<Section> lst, int planid, DateTime? dtBail)
        {
            IdMap result = [];
            Dictionary<string, Section> map = [];
            for (int ix = 0; ix < lst.Count; ix++)
            {
                Section s = lst[ix];
                string id = s.OfflineId ?? "error";
                if (!map.ContainsKey(id) && s.PlanId == planid)  //supporting sections will not be imported
                {
                    EntityEntry<Section>? t = dbContext.Sections.Add(s);
                    map.Add(id, t.Entity);
                }
                else
                    result.TryAdd(id, -1);
                if (dtBail != null && DateTime.Now > dtBail)
                    break;
            }
            dbContext.SaveChanges();

            foreach (KeyValuePair<string, Section> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private IdMap CopyPassages(List<Passage> lst, string mapKey, DateTime? dtBail)
        {
            IdMap result = [];
            Dictionary<string, Passage> map = [];
            for (int ix = 0; ix < lst.Count && (dtBail == null || DateTime.Now < dtBail); ix++)
            {
                Passage p = lst[ix];
                string id = p.OfflineId ?? "error";
                if (!map.ContainsKey(id))
                {
                    p.StepComplete = MapStepComplete(p.StepComplete, mapKey);
                    EntityEntry<Passage>? t = dbContext.Passages.Add(p);
                    map.Add(id, t.Entity);
                }
                else
                    result.TryAdd(id, -1);
            }
            dbContext.SaveChanges();

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
                if (!string.IsNullOrEmpty(u.Email))
                {
                    myu = dbContext.Users.FirstOrDefault(m => m.Email == u.Email && !m.Archived);
                    if (myu == null)
                        //invite these users to the org
                        UsersToInvite.Add(u.Email);
                }
                map.TryAdd(u.OfflineId ?? "error", myu?.Id ?? currentuser);
            }
            return map;
        }
#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
        private IdMap MapActivityStates(List<Activitystate> lst)
        {
            IdMap map = [];
            int done = dbContext.Activitystates.FirstOrDefault(m => m.State == "done")?.Id??0;
            foreach (Activitystate r in lst)
            {
                Activitystate? myr = dbContext.Activitystates.FirstOrDefault(m => m.State.ToLower() == r.State.ToLower());
                map.TryAdd(r.OfflineId ?? "error", myr?.Id ?? done);
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
                    map.TryAdd(r.OfflineId ?? "error", myr.Id);
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
                map.TryAdd(r.OfflineId ?? "error", myr?.Id ?? other);
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
                map.TryAdd(r.OfflineId ?? "error", myr?.Id ?? other);
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
                map.TryAdd(r.OfflineId ?? "error", myr?.Id ?? def);
            }
            return map;
        }
        private IdMap MapWorkflowsteps(List<Workflowstep> lst)
        {
            IdMap map = [];
            int def = dbContext.Workflowsteps.FirstOrDefault(m => m.Name == "Done" && m.Process == "OBT"  && !m.Archived)?.Id ?? 0;

            foreach (Workflowstep r in lst)
            {
                Workflowstep? myr = dbContext.Workflowsteps.FirstOrDefault(m => m.Name.ToLower() == r.Name.ToLower() && m.Process.ToLower() == r.Process.ToLower() && !m.Archived);
                map.TryAdd(r.OfflineId ?? "error", myr?.Id ?? def);
            }
            return map;
        }
        private IdMap MapIntegrations(List<Integration> lst)
        {
            IdMap map = [];
            foreach (Integration r in lst)
            {
                Integration? myr = dbContext.Integrations.FirstOrDefault(m => m.Name.ToLower() == r.Name.ToLower() && !m.Archived);
                if (myr != null)
                    map.TryAdd(r.OfflineId ?? "error", myr.Id);
            }
            return map;
        }
        private IdMap CopyArtifactCategorys(List<Artifactcategory> lst, int orgId)
        {
            Dictionary<string, Artifactcategory> map = [];
            foreach (Artifactcategory c in lst)
            {
                Artifactcategory? myc = dbContext.Artifactcategorys.FirstOrDefault(m => (m.OrganizationId == null || m.OrganizationId == orgId) && m.Categoryname == c.Categoryname && !m.Archived);
                string id = c.OfflineId ?? "error";
                if (myc == null)
                {
                    if (!map.ContainsKey(id))
                    {
                        EntityEntry<Artifactcategory>? t = dbContext.Artifactcategorys.Add(c);
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
        private IdMap CopyOrgSchemes(IList<Organizationscheme> lst, int orgId)
        {
            Dictionary<string, Organizationscheme> map = [];
            foreach (Organizationscheme s in lst)
            {
                string id = s.OfflineId ?? "error";
                Organizationscheme? ex = dbContext.Organizationschemes.Where(o => o.OrganizationId == orgId && o.Name == s.Name && !o.Archived).FirstOrDefault();
                if (ex != null)
                    map.Add(id, ex);
                else
                {
                    if (!map.ContainsKey(id))
                    {
                        EntityEntry<Organizationscheme>? t = dbContext.Organizationschemes.Add(s);
                        map.Add(id, t.Entity);
                    }
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Organizationscheme> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private IdMap CopyOrgSchemeSteps(IList<Organizationschemestep> lst)
        {
            Dictionary<string, Organizationschemestep> map = [];
            foreach (Organizationschemestep s in lst)
            {
                string id = s.OfflineId ?? "error";
                Organizationschemestep? ex = dbContext.Organizationschemesteps.Where(o => o.OrganizationschemeId == s.OrganizationschemeId && o.OrgWorkflowStepId == s.OrgWorkflowStepId && !o.Archived).FirstOrDefault();
                if (ex != null)
                    map.Add(id, ex);
                else
                {
                    if (!map.ContainsKey(id))
                    {
                        EntityEntry<Organizationschemestep>? t = dbContext.Organizationschemesteps.Add(
                            new Organizationschemestep
                            {
                                OrganizationschemeId = s.OrganizationschemeId,
                                OrgWorkflowStepId = s.OrgWorkflowStepId,
                                UserId = s.UserId,
                                GroupId = s.GroupId,
                            });
                        map.Add(id, t.Entity);
                    }
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Organizationschemestep> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }

        private IdMap CopyOrgKeyTerms(IList<Orgkeyterm> lst, int orgId)
        {
            Dictionary<string, Orgkeyterm> map = [];
            foreach (Orgkeyterm s in lst)
            {
                string id = s.OfflineId ?? "error";
                Orgkeyterm? ex = dbContext.Orgkeyterms.Where(o => o.OrganizationId == orgId && o.Term == s.Term && !o.Archived).FirstOrDefault();
                if (ex != null)
                    map.Add(id, ex);
                else
                {
                    if (!map.ContainsKey(id))
                    {
                        EntityEntry<Orgkeyterm>? t = dbContext.Orgkeyterms.Add(s);
                        map.Add(id, t.Entity);
                    }
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Orgkeyterm> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private static bool AreToolsEquivalent(string? tool1, string? tool2)
        {
            if (string.IsNullOrEmpty(tool1) && string.IsNullOrEmpty(tool2))
                return true;
            if (string.IsNullOrEmpty(tool1) || string.IsNullOrEmpty(tool2))
                return false;

            try
            {
                JObject json1 = JObject.Parse(tool1);
                JObject json2 = JObject.Parse(tool2);

                string? toolValue1 = json1["tool"]?.ToString();
                string? toolValue2 = json2["tool"]?.ToString();

                // Tool values must match
                if (toolValue1 != toolValue2)
                    return false;

                // If either has settings, compare them
                string? settings1 = json1["settings"]?.ToString();
                string? settings2 = json2["settings"]?.ToString();

                // If both have non-empty settings, they must match
                bool hasSettings1 = !string.IsNullOrEmpty(settings1);
                bool hasSettings2 = !string.IsNullOrEmpty(settings2);

                return (!hasSettings1 && !hasSettings2) || settings1 == settings2;
            }
            catch
            {
                return tool1 == tool2;
            }
        }

        private IdMap CopyOrgworkflowsteps(IList<Orgworkflowstep> lst, int orgId)
        {
            Dictionary<string, Orgworkflowstep> map = [];
            List<Orgworkflowstep> destSteps = [.. dbContext.Orgworkflowsteps.Where(o => o.OrganizationId == orgId && !o.Archived).OrderBy(o => o.Sequencenum)];
            foreach (Orgworkflowstep s in lst.OrderBy(o => o.Sequencenum))
            {
                string id =  s.OfflineId ?? "error";
                Orgworkflowstep? ex = destSteps.FirstOrDefault(o => AreToolsEquivalent(o.Tool, s.Tool));
                if (ex != null && !map.ContainsValue(ex))
                    map.Add(id, ex);
                else
                {
                    if (!map.ContainsKey(id))
                    {
                        string uniqueName = s.Name;
                        int tryn = 1;
                        while (destSteps.Any(o => o.Process == s.Process && o.Name == uniqueName))
                            uniqueName = s.Name + "_c" + tryn++;
                        s.Name = uniqueName;

                        EntityEntry<Orgworkflowstep>? t = dbContext.Orgworkflowsteps.Add(s);
                        destSteps.Add(t.Entity);
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
                string id = s.OfflineId ?? "error";
                Orgworkflowstep? ex = dbContext.Orgworkflowsteps.Where(o => o.OrganizationId == orgId && o.Name == s.Name && !o.Archived).FirstOrDefault();
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
        private IdMap CopyOrgkeytermtargets(IList<Orgkeytermtarget> lst)
        {
            Dictionary<string, Orgkeytermtarget> map = [];
            foreach (Orgkeytermtarget s in lst)
            {
                string id = s.OfflineId ?? "error";
                if (!map.ContainsKey(id))
                {
                    EntityEntry<Orgkeytermtarget>? t = dbContext.Orgkeytermtargets.Add(s);
                    map.Add(id, t.Entity);
                }
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Orgkeytermtarget> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private void CopyOrgkeytermreferences(IList<Orgkeytermreference> lst)
        {
            foreach (Orgkeytermreference s in lst)
            {
                string id = s.OfflineId ?? "error";
                EntityEntry<Orgkeytermreference>? t = dbContext.Orgkeytermreferences.Add(s);
            }
            dbContext.SaveChanges();
            return;
        }
        private IdMap CopyGraphics(List<Graphic> lst, string mapKey, IdMap oldmap, DateTime? dtBail = null)
        {
            for (int ix = 0; ix < lst.Count && (dtBail == null || DateTime.Now < dtBail); ix++)
            {
                Graphic g = lst[ix];
                string id = g.OfflineId ?? "error";
                if (!oldmap.ContainsKey(id))
                {
                    int? newResourceId = g.ResourceType switch
                    {
                        "passage" => GetMappedId(Tables.Passages, mapKey, g.ResourceId.ToString()) ?? g.ResourceId,
                        "category" => GetMappedId(Tables.ArtifactCategorys, mapKey, g.ResourceId.ToString()) ?? g.ResourceId,
                        "section" => GetMappedId(Tables.Sections, mapKey, g.ResourceId.ToString()) ?? g.ResourceId,
                        _ => throw new Exception($"Unknown graphic resource type '{g.ResourceType}' must be passage, section, or category")
                    };
                    int savedId = 0;
                    if (newResourceId != null && newResourceId != g.ResourceId)
                    {
                        g.ResourceId = (int)newResourceId;
                        if (!dbContext.Graphics.Where(og => og.OrganizationId == g.OrganizationId && og.ResourceType == g.ResourceType && og.ResourceId == g.ResourceId).Any())
                        {
                            EntityEntry<Graphic>? t = dbContext.Graphics.Add(g);
                            dbContext.SaveChanges();
                            savedId = t.Entity.Id;
                        }
                    }
                    if (savedId == 0)
                    {
                        Graphic? e = dbContext.Graphics.Where(og => og.OrganizationId == g.OrganizationId && og.ResourceType == g.ResourceType && og.ResourceId == g.ResourceId).FirstOrDefault();
                        savedId = e?.Id ?? 0;
                    }
                    SaveId(Tables.Graphics, id, savedId, mapKey);
                    oldmap.Add(id, savedId);
                }
            }
            return oldmap;
        }

        private void CopyIP(List<Intellectualproperty> lst)
        {
            foreach (Intellectualproperty ip in lst)
            {
                _ = dbContext.IntellectualPropertys.Add(ip);
            }
        }
        private IdMap CopySectionResources(List<Sectionresource> lst, int orgId, int projectId, DateTime? dtBail)
        {
            Dictionary<string, Sectionresource> map = [];
            int internalize= dbContext.Orgworkflowsteps.Where(s => s.OrganizationId == orgId && !s.Archived).ToList().Where(s => s.Tool.Contains("{\"tool\": \"resource")).FirstOrDefault()?.Id ?? 0;
            for (int ix = 0; ix < lst.Count && (dtBail == null || DateTime.Now < dtBail); ix++)
            {
                Sectionresource sr = lst[ix];
                int stepId = sr.OrgWorkflowStepId;
                if (stepId == 0)
                    stepId = internalize;
                int? m = sr.MediafileId;
                if (sr.MediafileId != null && (m ?? 0) == 0)
                {
                    Console.WriteLine($"Mediafile {sr.MediafileId} not found");
                    sr.MediafileId = null;
                }
                sr.ProjectId = projectId;
                string id = sr.OfflineId ?? "error";
                if (!map.ContainsKey(id))
                {
                    EntityEntry<Sectionresource>? t =  dbContext.Sectionresources.Add(sr);
                    map.Add(id, t.Entity);
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
        private IdMap CopySharedResources(List<Sharedresource> lst, IdMap srmap, DateTime? dtBail)
        {
            IdMap.KeyCollection alreadydone = srmap.Keys;
            Dictionary<string, Sharedresource> map = [];
            IdMap result = [];

            for (int ix = 0; ix < lst.Count; ix++)
            {
                Sharedresource sr = lst[ix];
                string id = sr.OfflineId ?? "error";
                if (id == "error")
                    continue;

                if (sr.PassageId != null && !alreadydone.Contains(id) && !map.ContainsKey(id) && !dbContext.Sharedresources.Where(r => r.PassageId == sr.PassageId && r.Note == sr.Note).Any())
                {
                    EntityEntry<Sharedresource>? t = dbContext.Sharedresources.Add(sr);
                    map.Add(id, t.Entity);
                }
                else
                {
                    result.TryAdd(id, -1);
                    Console.WriteLine($"Skipping shared resource with id {id} because it already exists in the map or database");
                }
                if (dtBail != null && DateTime.Now > dtBail)
                    break;
            }
            dbContext.SaveChanges();
            foreach (KeyValuePair<string, Sharedresource> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private void CopySharedResourceReferences(IList<Sharedresourcereference> lst)
        {
            foreach (Sharedresourcereference srr in lst)
            {
                _ = dbContext.Sharedresourcereferences.Add(srr);
            }
            dbContext.SaveChanges();
        }
        private string? MapStepComplete(string? source, string? mapKey)
        {
            if (source == null)
                return null;
            if (string.IsNullOrEmpty(mapKey)) //same org
                return source;
            JObject result = JObject.Parse(source ?? "{}");
            JToken? js = result["completed"];
            if (js != null && js.Type == JTokenType.Array)
            {
                foreach (JToken entry in js.Children())
                {
                    if (int.TryParse(entry["stepid"]?.ToString(), out int id))
                        entry["stepid"] = GetMappedId(Tables.OrgWorkflowSteps, mapKey, id.ToString()).ToString();
                }
                return result.ToString();
            }
            return null;
        }
        private async Task<IdMap> CopyMediafilesAsync(List<Mediafile> lst, Plan plan,
            string mapKey, DateTime? dtBail, ZipArchive? archive)
        {
            //the lst has already skipped the ones in oldmap
            //the planid in the lst is still the old one
            IdMap oldmap = GetMediafileMap(mapKey);
            string suffix = "_" + plan.Slug;
            for (int ix = 0; ix < lst.Count && (dtBail == null || DateTime.Now < dtBail); ix++)
            {
                Mediafile m = lst[ix];
                string id = m.OfflineId ?? "error";
                if (!oldmap.ContainsKey(id))
                {
                    if (m.SourceMedia == null && m.SourceMediaId != null)
                    {
                        m.OfflineSourceMediaId = m.SourceMediaId.ToString();
                        m.SourceMediaId = null;
                    }
                    if (string.IsNullOrEmpty(m.OriginalFile) && !string.IsNullOrEmpty(m.AudioUrl)) //OneStory scrape looked like this
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
                            m.OriginalFile = audioUrl[(lastSlashIndex + 1)..];
                        }
                    }
                    string? originalS3File = m.S3File??"";
                    int oldPlan = m.PlanId;
                    m.PlanId = plan.Id;
                    //if it's not biblebrain or aquifer - make a copy
                    string audiourl = m.AudioUrl??"";
                    bool centralCopy = audiourl.Contains("biblebrain") || audiourl.Contains("aquifer");
                    bool copyIt = !centralCopy;

                    //if we have a file we might not have the biblebrain or aquifer file
                    if (archive != null)
                    {
                        if (centralCopy)
                            copyIt = !await _S3Service.FileExistsAsync(m.S3File ?? "junk", mediaService.DirectoryName(m));
                        else
                            m.S3File = await mediaService.GetNewFileNameAsync(m, suffix);
                        if (copyIt)
                        {
                            await CopyMediaFile(originalS3File, m, archive);
                        }
                    }
                    else if (copyIt)
                    {
                        m.S3File = await mediaService.GetNewFileNameAsync(m, suffix);
                        await CopyMediafile(originalS3File, oldPlan, m);
                    }

                    EntityEntry<Mediafile>? t =  dbContext.Mediafiles.Add(m);
                    //save as we go in case we have to resume
                    dbContext.SaveChanges();
                    SaveId(Tables.Mediafiles, id, t.Entity.Id, mapKey);
                    oldmap.Add(id, t.Entity.Id);
                    dbContext.SaveChanges();
                }
            }
            return oldmap;
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
        private IdMap CopyDiscussions(List<Discussion> lst, DateTime? dtBail)
        {
            Dictionary<string, Discussion> map = [];
            for (int ix = 0; ix < lst.Count && (dtBail == null || DateTime.Now < dtBail); ix++)
            {
                Discussion d = lst[ix];
                string id = d.OfflineId ?? "error";

                EntityEntry<Discussion>? t = dbContext.Discussions.Add(d);
                map.Add(id, t.Entity);
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Discussion> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }
        private IdMap CopyComments(List<Comment> lst, DateTime? dtBail)
        {
            Dictionary<string, Comment> map = [];
            //saving map just to keep track of where we are
            for (int ix = 0; ix < lst.Count && (dtBail == null || DateTime.Now < dtBail); ix++)
            {
                Comment c = lst[ix];
                string id = c.OfflineId ?? "error";
                EntityEntry<Comment> t = dbContext.Comments.Add(c);
                map.TryAdd(id, t.Entity);
            }
            dbContext.SaveChanges();
            IdMap result = [];
            foreach (KeyValuePair<string, Comment> kvp in map)
                result.TryAdd(kvp.Key, kvp.Value.Id);
            return result;
        }

        private IdMap GetMediafileMap(string newProjId)
        {
            MediafileMap ??= GetMap(Tables.Mediafiles, newProjId);
            return MediafileMap;
        }
        private IdMap GetUserMap(string newProjId)
        {
            UserMap ??= GetMap(Tables.Users, newProjId) ?? [];
            return UserMap;
        }
        private void SaveId(string table, string oldId, int newId, string mapKey, bool Save = true)
        {
            dbContext.Copyprojects.Add(new CopyProject()
            {
                Sourcetable = table,
                Newprojid = mapKey,
                Oldid = oldId,
                Newid = newId
            });
            if (Save)
                dbContext.SaveChanges();
        }
        private void SaveMap(IdMap map, string table, string newProjId)
        {
            foreach (KeyValuePair<string, int> kvp in map)
            {
                SaveId(table, kvp.Key, kvp.Value, newProjId, false);
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
        private int? GetMappedId(string table, string projId, string? oldId)
        {
            if (projId == "" || string.IsNullOrEmpty(oldId))
                return null;
            if (!table.EndsWith('s'))
                table += "s";
            CopyProject? cp = dbContext.Copyprojects.Where(c => c.Newprojid == projId && c.Sourcetable == table.ToLower() && c.Oldid == oldId).FirstOrDefault();
            int id = 0;
            if (cp == null)
                _ = int.TryParse(oldId, out id);
            return cp?.Newid ?? id;
        }
        //DEPRECATED
        private async Task<Fileresponse> ProcessImportCopyProjectDeprecatedAsync(
                Project sourceproject,
                bool sameOrg,
                int start,
                string? projId)
        {
            DateTime dtBail = DateTime.Now.AddSeconds(60);
            User currentuser = CurrentUser() ?? new User();
            IQueryable<Organization> sourceOrg = dbContext.Organizations.Where(o => o.Id == sourceproject.OrganizationId && !o.Archived);
            int orgid = sameOrg ? sourceOrg.FirstOrDefault()?.Id ?? 0 : 0;
            return await ProcessImportCopyProjectAsync(sourceproject, orgid, start, projId);
        }
        private async Task<Fileresponse> ProcessImportCopyProjectAsync(
                Project sourceproject,
                int orgId,
                int start,
                string? projId)
        {
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            User currentuser = CurrentUser() ?? new User();
            bool sameOrg =  sourceproject.OrganizationId == orgId;
            IQueryable<Organization>? sourceOrg =  dbContext.Organizations.Where(o => o.Id == sourceproject.OrganizationId && !o.Archived);
            Organization? org = orgId > 0 ? dbContext.Organizations.Where(o => o.Id == orgId && !o.Archived).FirstOrDefault() : null;
            IQueryable<Plan> sourceplans = dbContext.Plans.Where(p=>p.ProjectId == sourceproject.Id && !p.Archived);
            Project project = new();
            Plan plan = new();
            string mapKey = projId??"";

            if (start == 0)
            {
                org ??= await CreateNewOrg(sourceOrg.FirstOrDefault() ?? throw new Exception("Org not found"), false, false, currentuser);
                project = CreateNewProject(sourceproject, false, org.Id, currentuser);
                plan = await CreateNewPlan(sourceplans.First(), project, currentuser);
                mapKey = project.Id.ToString();
                SaveId(Tables.Organizations, sourceproject.OrganizationId.ToString(), org.Id, mapKey);
                SaveId(Tables.Plans, sourceplans.First().Id.ToString(), plan.Id, mapKey);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                org = dbContext.Organizations.Where(o => o.Id == GetSingleId(Tables.Organizations, mapKey)).FirstOrDefault();
                if (org == null)
                    return ErrorResponse("Can't find new organization", sourceproject.Name);
                //use tmpProj so project remains not nullable
                if (!int.TryParse(mapKey, out int newId))
                    newId = GetSingleId(Tables.Projects, mapKey);
                Project? tmpProj = dbContext.Projects.Where(p => p.Id == newId).FirstOrDefault();
                if (tmpProj == null)
                    return ErrorResponse("Can't find new project", sourceproject.Name);
                project = tmpProj;
                Plan? tmpPlan = dbContext.Plans.Where(p => p.ProjectId == newId && !p.Archived).FirstOrDefault();
                if (tmpPlan == null)
                    return ErrorResponse("Can't find new plan", sourceproject.Name);
                plan = tmpPlan;
            }
            HttpContext?.SetFP("copy project");
            try
            {
                int origPlan = sourceplans.First().Id;
                IQueryable<Section> sourcesections = dbContext.Sections.Where(x => x.PlanId == origPlan && !x.Archived).OrderBy(s => s.Id);
                IQueryable<Passage> sourcepassages = sourcesections.Join(dbContext.Passages, s => s.Id, p=> p.SectionId, (s, p) => p).Where(x => !x.Archived).OrderBy(p => p.Id);
                IQueryable<Sectionresource> sectionresources = SectionResources(sourcesections);

                IEnumerable<Mediafile> sourcemediafiles = PlanSourceMedia(sectionresources).Where(m => m.PlanId == origPlan);

                IQueryable<Orgkeytermtarget> oktt = sameOrg ? dbContext.Orgkeytermtargets.Where(s => s.Id == -1) :
                                                              dbContext.Orgkeytermtargets.Where(s => s.OrganizationId == sourceproject.OrganizationId);
                IQueryable<Artifactcategory> categories = sameOrg ? dbContext.Artifactcategorys.Where(ac => ac.Id == -1) :
                                                                    dbContext.Artifactcategorys.Where(ac => !ac.Archived && ( ac.OrganizationId == null || ac.OrganizationId == sourceproject.OrganizationId));
                //Do not copy mediafiles for shared notes
                //IQueryable<VWProject> sharednotes = dbContext.VWProjects.Where(x => x.ProjectId == sourceproject.Id && x.SharedResourceId != null);
                IQueryable<Note> supportingNotes = dbContext.Notes.Where(n => n.Id == -1);
                //        .Join(sharednotes, n => n.ResourceId, sn => sn.SharedResourceId, (n, sn) => n);
                IQueryable<Intellectualproperty>? ip = sameOrg ? dbContext.IntellectualPropertys.Where(ip => ip.Id == -1) :
                                                                OrgIPs(dbContext.Organizations.Where(o => o.Id == sourceproject.OrganizationId));
                IQueryable<Bible>  orgBibles = dbContext.BiblesData.Where(b => b.Id == -1); //don't copy bibles data

                List<Mediafile> pm = ProjectMedia(oktt, categories, sectionresources, ip, sourceplans, supportingNotes, orgBibles);
                IEnumerable<Mediafile> myMedia = pm.Where(m => m.PlanId == origPlan);

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
                                List<Artifactcategory> acs = [.. categories.ToList().Select(c => new Artifactcategory
                                {
                                    OrganizationId = org.Id,
                                    Categoryname = c.Categoryname,
                                    Discussion = c.Discussion,
                                    Resource = c.Resource,
                                    Note = c.Note,
                                    OfflineTitleMediafileId = c.TitleMediafileId.ToString(),
                                    OfflineId = c.StringId
                                })];
                                SaveMap(CopyArtifactCategorys(acs, org.Id), name, mapKey);
                            }
                            ix++;
                            break;

                        case Tables.IntellectualPropertys: //I so mediafiles is done
                            if (!sameOrg)
                            {
                                //copy but change the organization to current org
                                List<Intellectualproperty> ips = [.. ip.ToList().Select(ip => new Intellectualproperty
                                {
                                    RightsHolder = ip.RightsHolder,
                                    Notes = ip.Notes,
                                    OfflineMediafileId = null,
                                    OrganizationId = org.Id,
                                    ReleaseMediafileId = GetMappedId(Tables.Mediafiles, mapKey, ip.ReleaseMediafileId.ToString()),
                                    OfflineId = ip.StringId,
                                })];
                                CopyIP(ips);
                            }
                            ix++;
                            break;

                        case Tables.OrgWorkflowSteps:
                            if (!sameOrg)
                            {
                                List<Orgworkflowstep> ows = [.. dbContext.Orgworkflowsteps.Where(s => s.OrganizationId == sourceproject.OrganizationId && !s.Archived).ToList().Select(s =>
                                new Orgworkflowstep
                                {
                                    OrganizationId= org.Id,
                                    Process = s.Process,
                                    Name = s.Name,
                                    Sequencenum= s.Sequencenum,
                                    Tool = s.Tool,
                                    Permissions = s.Permissions,  //will this need to be adapted???  TODO
                                    OfflineId = s.StringId,
                                })];
                                SaveMap(CopyOrgworkflowsteps(ows, org.Id), name, mapKey);
                            }
                            ix++;
                            break;
                        case Tables.OrgKeyTerms:
                            if (!sameOrg)
                            {
                                List<Orgkeyterm> oks = [.. dbContext.OrgKeytermsData.Where(a => a.OrganizationId == sourceproject.OrganizationId && !a.Archived).ToList().Select(s =>
                                    new Orgkeyterm
                                    {
                                        OrganizationId= org.Id,
                                        Term = s.Term,
                                        Domain = s.Domain,
                                        Definition = s.Definition,
                                        Category = s.Category,
                                        OfflineId = s.StringId
                                    })];
                                SaveMap(CopyOrgKeyTerms(oks, org.Id), name, mapKey);
                            }
                            ix++;
                            break;
                        case Tables.OrgKeyTermReferences:
                            if (!sameOrg)
                            {
                                List<Orgkeytermreference> ktr =[.. dbContext.Orgkeytermreferences
                                                                    .Join(dbContext.OrgKeytermsData.Where(a => a.OrganizationId == sourceproject.OrganizationId && !a.Archived),
                                                                    r => r.OrgkeytermId, t => t.Id, (r,t) => r).ToList().Select(s =>
                                    new Orgkeytermreference
                                    {
                                        OrgkeytermId = GetMappedId(Tables.OrgKeyTerms, mapKey, s.OrgkeytermId.ToString()) ?? 0,
                                        ProjectId = project.Id,
                                        SectionId = GetMappedId(Tables.Sections, mapKey, s.SectionId.ToString()) ?? 0,
                                        OfflineId = s.StringId
                                    })];
                                CopyOrgkeytermreferences(ktr);
                            }
                            ix++;
                            break;
                        case Tables.OrgKeyTermTargets:
                            if (!sameOrg)
                            {
                                List<Orgkeytermtarget> ktt =[.. dbContext.Orgkeytermtargets.Where(s => s.OrganizationId == sourceproject.OrganizationId).ToList().Select(s =>
                                        new Orgkeytermtarget
                                        {
                                            OrganizationId= org.Id,
                                            Term = s.Term,
                                            TermIndex = s.TermIndex,
                                            Target = s.Target,
                                            MediafileId = GetMediafileMap(mapKey).GetValueOrDefault(s.MediafileId),
                                            OfflineId = s.StringId
                                        })];
                                CopyOrgkeytermtargets(ktt);
                            }
                            ix++;
                            break;

                        case Tables.Sections:
                            IdMap sectmap =  GetMap(Tables.Sections, mapKey);
                            int totalCount = sourcesections.Count();
                            while (sectmap.Count < totalCount && DateTime.Now < dtBail)
                            {
                                List<Section> sourceSectionChunk = [.. sourcesections.Skip(sectmap.Count).Take(DataChunkSize)];

                                List<Section> sections =[.. sourceSectionChunk.Select(s =>
                                    new Section
                                    {
                                        Name = s.Name,
                                        PlanId = plan.Id,
                                        Sequencenum = s.Sequencenum,
                                        State = s.State,
                                        Level = s.Level,
                                        Published = false,
                                        PublishTo = "{}",
                                        OrganizationSchemeId = GetMappedId(Tables.OrganizationSchemes, mapKey, s.OrganizationSchemeId.ToString()), //permissions
                                        GroupId = null, //??permissions
                                        EditorId = null, //deprecated permissions
                                        TranscriberId  = null, //deprecated permissions
                                        //mediafiles haven't been done...so do titlemediafiles later
                                        OfflineTitleMediafileId = s.TitleMediafileId.ToString(),
                                        OfflineId = s.StringId
                                    })];
                                IdMap newids = CopySections(sections, plan.Id, dtBail);
                                SaveMap(newids, name, mapKey);
                                sectmap = sectmap.Union(newids).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (sectmap.Count == totalCount)
                                ix++;
                            status = string.Format("{0} {1}/{2}", status, sectmap.Count, totalCount);
                            break;

                        case Tables.Passages:
                            IdMap psgmap =  GetMap(Tables.Passages, mapKey);
                            IQueryable<Passage> sourcepsg = sourcesections.Join(dbContext.Passages, s => s.Id, p=> p.SectionId, (s, p) => p).Where(x => !x.Archived);
                            int psgCount = sourcepsg.Count();
                            while (psgmap.Count < psgCount && DateTime.Now < dtBail)
                            {
                                List<Passage> sourcePassageChunk = [.. sourcepsg.Skip(psgmap.Count).Take(DataChunkSize)];

                                List<Passage> passages =[.. sourcePassageChunk.Select(p =>
                                new Passage
                                {
                                    Sequencenum = p.Sequencenum,
                                    Book = p.Book,
                                    Reference = p.Reference,
                                    Hold = p.Hold,
                                    Title = p.Title,
                                    SectionId = GetMappedId(Tables.Sections, mapKey, p.SectionId.ToString()) ?? 0,
                                    State = null,
                                    LastComment = "",
                                    PassagetypeId =  p.PassagetypeId,
                                    SharedResourceId = p.SharedResourceId, //from our db so just copy it
                                    OfflineId = p.StringId,
                                    StepComplete=p.StepComplete,
                                })];
                                IdMap newids = CopyPassages(passages, mapKey, dtBail);
                                SaveMap(newids, name, mapKey);
                                psgmap = psgmap.Union(newids).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (psgmap.Count == psgCount)
                                ix++;
                            status = string.Format("{0} {1}/{2}", status, psgmap.Count, psgCount);
                            break;

                        case Tables.SectionResources:
                            IdMap srMap = GetMap(Tables.SectionResources, mapKey);
                            int srCount = sectionresources.Count();
                            while (srMap.Count < srCount && DateTime.Now < dtBail)
                            {
                                IEnumerable<Sectionresource> tmpchunk = sectionresources.Skip(srMap.Count).Take(DataChunkSize);
                                List<Sectionresource> srs = [.. tmpchunk.ToList().Select(r =>
                                new Sectionresource
                                {
                                    SequenceNum = r.SequenceNum,
                                    Description = r.Description,
                                    SectionId = GetMappedId(Tables.Sections, mapKey, r.SectionId.ToString()) ?? 0,
                                    MediafileId = GetMediafileMap(mapKey).GetValueOrDefault(r.MediafileId),
                                    PassageId = GetMappedId(Tables.Passages, mapKey, r.PassageId?.ToString()),
                                    OrgWorkflowStepId = !sameOrg ? GetMappedId(Tables.OrgWorkflowSteps, mapKey, r.OrgWorkflowStepId.ToString()) ?? 0 : r.OrgWorkflowStepId,
                                    OfflineId = r.StringId
                                })];
                                IdMap newids = CopySectionResources(srs, org.Id, project.Id, dtBail);
                                SaveMap(newids, name, mapKey);
                                srMap = srMap.Union(newids).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (srMap.Count == srCount)
                                ix++;
                            else
                                status = string.Format("{0} {1}/{2}", status, srMap.Count, srCount);
                            break;

                        case Tables.SectionResourceUsers:
                            //don't copy the user completion info
                            ix++;
                            break;

                        case Tables.Mediafiles:
                            List<Mediafile> allSourceMedia = [.. myMedia.Distinct()];
                            int totalMediaCount = allSourceMedia.Count;
                            IdMap mfMap = GetMediafileMap(mapKey);
                            while (mfMap.Count < totalMediaCount && DateTime.Now < dtBail)
                            {
                                int skip = mfMap.Count;
                                IEnumerable<Mediafile> tmpchunk = allSourceMedia.Skip(skip).Take(MediafileChunkSize);
                                List<Mediafile> chunk = [..tmpchunk.Select(m =>
                                    new Mediafile
                                    {
                                        PassageId = GetMappedId(Tables.Passages, mapKey, m.PassageId?.ToString()),
                                        VersionNumber = Convert.ToBoolean(m.VersionNumber) ? m.VersionNumber : 1,
                                        AudioUrl = m.AudioUrl,
                                        EafUrl = m.EafUrl,
                                        Duration = m.Duration,
                                        ContentType = m.ContentType,
                                        //AudioQuality = m.AudioQuality,
                                        //TextQuality = m.TextQuality,
                                        Transcription = m.Transcription,
                                        PlanId = m.PlanId, //don't map this here - we need to know the old one to find the original file
                                        OriginalFile = m.OriginalFile ?? m.S3File,
                                        Filesize = m.Filesize,
                                        Position = 0,
                                        Segments = m.Segments,
                                        Languagebcp47 = m.Languagebcp47,
                                        Link = Convert.ToBoolean(m.Link),
                                        PerformedBy = m.PerformedBy,
                                        ReadyToShare = false,
                                        SourceSegments = m.SourceSegments,
                                        Transcriptionstate = m.Transcriptionstate,
                                        Topic = m.Topic,
                                        S3Folder = m.S3Folder,
                                        S3File = m.S3File,
                                        PublishedAs = null,
                                        PublishTo = "{}",
                                        ArtifactTypeId = CheckValidId(m.ArtifactTypeId),
                                        ArtifactCategoryId = CheckValidId(m.ArtifactCategoryId) == null ? null :
                                                     sameOrg ? m.ArtifactCategoryId : GetMappedId(Tables.ArtifactCategorys, mapKey, m.ArtifactCategoryId.ToString()),
                                        ResourcePassageId = CheckValidId(m.ResourcePassageId) == null ? null :
                                                     GetMappedId(Tables.Passages, mapKey, m.ResourcePassageId?.ToString() ?? ""),
                                        RecordedbyUser = m.RecordedbyUser ?? CurrentUser(),
                                        OfflineSourceMediaId = m.SourceMediaId?.ToString(),
                                        OfflineId = m.StringId,
                                    }
                                )];
                                mfMap = await CopyMediafilesAsync(chunk, plan, mapKey, dtBail, null);
                            }
                            MediafileMap = mfMap;
                            if (mfMap.Count == totalMediaCount)
                                ix++;
                            else
                                status = string.Format("{0} {1}/{2}", status, mfMap.Count, totalMediaCount);
                            break;

                        case Tables.PassageStateChanges:
                            //don't copy the passage state changes
                            ix++;
                            break;

                        case Tables.Discussions:
                            IQueryable<Discussion> sourcedesc = PlanDiscussions(PlanMedia(sourceplans));
                            int desccount = sourcedesc.Count();
                            IdMap dmap = GetMap(Tables.Discussions, mapKey);
                            while (dmap.Count < desccount && DateTime.Now < dtBail)
                            {
                                int skip = dmap.Count;
                                List<Discussion> sourceDiscussionChunk = [.. sourcedesc.Skip(skip).Take(DataChunkSize)];

                                List<Discussion> discussions =[.. sourceDiscussionChunk.Select(d =>
                                new Discussion
                                {
                                    ArtifactCategoryId = CheckValidId(d.ArtifactCategoryId) == null ? null : sameOrg ? d.ArtifactCategoryId : GetMappedId(Tables.ArtifactCategorys, mapKey, d.ArtifactCategoryId.ToString() ?? ""),
                                    MediafileId = CheckValidId(d.MediafileId) == null ? null : GetMediafileMap(mapKey).GetValueOrDefault(d.MediafileId),
                                    OrgWorkflowStepId = CheckValidId(d.OrgWorkflowStepId) == null ? throw new ArgumentException("Invalid OrgWorkflowStepId") : sameOrg ? d.OrgWorkflowStepId : GetMappedId(Tables.OrgWorkflowSteps, mapKey, d.OrgWorkflowStepId.ToString() ?? "") ?? 0,
                                    GroupId = sameOrg ? CheckValidId(d.GroupId) : null,
                                    Resolved = d.Resolved,
                                    Segments = d.Segments,
                                    Subject = d.Subject,
                                    UserId = GetMappedId(Tables.Users, mapKey, d.User?.Id.ToString()) ?? d.User?.Id,
                                    DateCreated = d.DateCreated,
                                    DateUpdated = DateTime.UtcNow,
                                    OfflineId = d.StringId,
                                 })];
                                IdMap newids = CopyDiscussions(discussions,  dtBail);
                                SaveMap(newids, name, mapKey);
                                dmap = dmap.Union(newids).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (dmap.Count == desccount)
                                ix++;
                            else
                                status = string.Format("{0} {1}/{2}", status, dmap.Count, desccount);

                            break;

                        case Tables.Comments:
                            IQueryable<Comment> sourcecomments = dbContext.Comments
                                .Join(PlanDiscussions(PlanMedia(sourceplans)), c => c.DiscussionId, d => d.Id, (c, d) => c)
                                .Where(x => !x.Archived);
                            int commentsCount = sourcecomments.Count();
                            IdMap cmap = GetMap(Tables.Comments, mapKey);
                            while (cmap.Count < commentsCount && DateTime.Now < dtBail)
                            {
                                int skip = cmap.Count;
                                List<Comment> sourceCommentChunk = [.. sourcecomments.Skip(skip).Take(DataChunkSize)];
                                List<Comment> comments = [.. sourceCommentChunk.Select(c =>
                                new Comment
                                {
                                    DiscussionId = GetMappedId(Tables.Discussions, mapKey, c.DiscussionId.ToString()) ?? 0,
                                    MediafileId = GetMappedId(Tables.Mediafiles, mapKey, c.MediafileId?.ToString() ?? ""),
                                    OfflineMediafileId = null,
                                    OfflineDiscussionId = null,
                                    CommentText = c.CommentText,
                                    Visible = c.Visible,
                                    OfflineId = c.StringId,
                                    CreatorUserId = GetMappedId(Tables.Users, mapKey, (c.CreatorUserId ?? currentuser.Id).ToString())
                                })];
                                IdMap newids = CopyComments(comments,  dtBail);
                                SaveMap(newids, name, mapKey);
                                cmap = cmap.Union(newids).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (cmap.Count == commentsCount)
                                ix++;
                            else
                                status = string.Format("{0} {1}/{2}", status, cmap.Count, commentsCount);
                            break;

                        case Tables.Graphics:
                            IQueryable<Graphic> allGraphics = dbContext.Graphics.Where(g => g.OrganizationId == sourceproject.OrganizationId && !g.Archived);
                            int graphicsCount = allGraphics.Count();
                            IdMap gmap = GetMap(name, mapKey);
                            while (gmap.Count < graphicsCount && DateTime.Now < dtBail)
                            {
                                List<Graphic> sourceGraphicChunk = [.. allGraphics.Skip(gmap.Count).Take(DataChunkSize)];
                                List<Graphic> graphics = [.. sourceGraphicChunk.Select(g =>
                                new Graphic
                                {
                                    OrganizationId = org.Id,
                                    MediafileId = GetMappedId(Tables.Mediafiles, mapKey, g.MediafileId.ToString()),
                                    ResourceType = g.ResourceType,
                                    ResourceId = g.ResourceId,
                                    Info = g.Info,
                                    OfflineId = g.StringId,
                                })];
                                gmap = CopyGraphics(graphics, mapKey, gmap, dtBail);
                            }
                            if (gmap.Count == graphicsCount)
                                ix++;
                            else
                                status = string.Format("{0} {1}/{2}", status, gmap.Count, graphicsCount);
                            break;

                        case Tables.OrganizationSchemes:
                            if (!sameOrg)
                            {
                                List<Organizationscheme> oslist = [.. dbContext.Organizationschemes.Where(s => s.OrganizationId == sourceproject.OrganizationId && !s.Archived).ToList().Select(s =>
                                    new Organizationscheme
                                    {
                                        OrganizationId= org.Id,
                                        Name = s.Name,
                                        OfflineId = s.StringId,
                                    })];
                                SaveMap(CopyOrgSchemes(oslist, org.Id), name, mapKey);
                            }
                            ix++;
                            break;

                        case Tables.OrganizationSchemeSteps:
                            if (!sameOrg)
                            {
                                List<Organizationschemestep> ossList = [.. dbContext.Organizationschemesteps
                                                                            .Join(dbContext.Organizationschemes.Where(s => s.OrganizationId == sourceproject.OrganizationId && !s.Archived),
                                                                            s => s.OrganizationschemeId, os => os.Id,
                                                                            (s, os) => s).Where(s => !s.Archived).ToList().Select(s =>
                                    new Organizationschemestep
                                    {
                                        OrganizationschemeId = GetMappedId(Tables.OrganizationSchemes, mapKey, s.OrganizationschemeId.ToString()) ?? 0,
                                        OrgWorkflowStepId = GetMappedId(Tables.OrgWorkflowSteps, mapKey, s.OrgWorkflowStepId.ToString()) ?? 0,
                                        UserId = GetMappedId(Tables.Users, mapKey, s.UserId?.ToString()),
                                        GroupId = GetMappedId(Tables.Groups, mapKey, s.GroupId?.ToString()),
                                        OfflineId = s.StringId
                                    })];
                                CopyOrgSchemeSteps(ossList);
                            }
                            ix++;
                            break;

                        //we are on same database so don't need to copy shared ones but we do need to copy project notes
                        case Tables.SharedResources:
                            IQueryable<Sharedresource> sourceSharedResources = dbContext.Sharedresources
                                .Join(sourcepassages, sr => sr.PassageId, p => p.Id, (sr, p) => sr)
                                .Where(sr => !sr.Archived).OrderBy(sr => sr.Id);
                            int shrCount = sourceSharedResources.Count();
                            IdMap shrMap = GetMap(Tables.SharedResources, mapKey);
                            while (shrMap.Count < shrCount && DateTime.Now < dtBail)
                            {
                                int skip = shrMap.Count;
                                List<Sharedresource> sourceSharedResourceChunk = [.. sourceSharedResources.Skip(skip).Take(DataChunkSize)];
                                List<Sharedresource> srList = [.. sourceSharedResourceChunk.Select(sr =>
                                    new Sharedresource
                                    {
                                        PassageId = GetMappedId(Tables.Passages, mapKey, sr.PassageId?.ToString()),
                                        //ClusterId = clusterId,
                                        Title = sr.Title,
                                        Description = sr.Description,
                                        Languagebcp47 = sr.Languagebcp47,
                                        TermsOfUse = sr.TermsOfUse,
                                        Keywords = sr.Keywords,
                                        ArtifactCategoryId = GetMappedId(Tables.ArtifactCategorys, mapKey, sr.ArtifactCategoryId?.ToString()),
                                        TitleMediafileId = GetMappedId(Tables.Mediafiles, mapKey, sr.TitleMediafileId?.ToString()),
                                        Note = sr.Note,
                                        LinkUrl = sr.LinkUrl,
                                        OfflineId = sr.StringId,
                                    })];
                                IdMap newids = CopySharedResources(srList, shrMap, dtBail);
                                SaveMap(newids, name, mapKey);
                                shrMap = shrMap.Union(newids).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (shrMap.Count == shrCount)
                                ix++;
                            else
                                status = string.Format("{0} {1}/{2}", status, shrMap.Count, shrCount);
                            break;

                        case Tables.SharedResourceReferences:
#pragma warning disable CS8602 // Dereference of a possibly null reference.  I know Sharedresource won't be null
                            List<Sharedresourcereference> srrList = [.. dbContext.SharedresourcereferencesData
                                                            .Join(sourcepassages, srr => srr.SharedResource.PassageId, p => p.Id, (srr, p) => srr)
                                                            .Where(srr => !srr.Archived).ToList().Select(srr =>
                                                                        new Sharedresourcereference
                                                                {
                                                                    SharedResourceId = GetMappedId(Tables.SharedResources, mapKey, srr.SharedResourceId.ToString()) ?? 0,
                                                                    Book = srr.Book,
                                                                    Chapter = srr.Chapter,
                                                                    Verses = srr.Verses,
                                                                    OfflineId = srr.StringId,
                                                                })];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                            CopySharedResourceReferences(srrList);
                            ix++;
                            break;

                        default:
                            ix++;
                            break;
                    }
                } while (DateTime.Now < dtBail && ix < TableOrder.Count)
                                ;
                _ = dbContext.SaveChanges();
                bool complete = (ix == TableOrder.Count);
                if (complete)
                {
                    FixEarlyIds(mapKey);
                }
                return new Fileresponse()
                {
                    Id = complete ? -1 : ix,
                    Message = complete ? string.Format("{0}: {1}", org.Name, project.Name) : status,
                    FileURL = mapKey.ToString(),
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
                    mapKey.ToString()
                );
            }

        }
        private void FixEarlyIds(string mapKey)
        {
            //These tables are processed before mediafiles so update their titles now
            //fix the title media for artifact categories
            List<Artifactcategory> cats = [.. dbContext.Copyprojects.Where(c => c.Sourcetable == Tables.ArtifactCategorys && c.Newprojid == mapKey)
                                            .Join(dbContext.Artifactcategorys, cp => cp.Newid, ac => ac.Id, (cp, ac) => ac).Where(ac => ac.OfflineTitleMediafileId != null)];
            cats.ForEach(n => {
                n.TitleMediafileId = GetMappedId(Tables.Mediafiles, mapKey, n.OfflineTitleMediafileId);
            });
            dbContext.Artifactcategorys.UpdateRange(cats);
            List<Section> sections = [.. dbContext.Copyprojects.Where(c => c.Sourcetable == Tables.Sections && c.Newprojid == mapKey)
                                        .Join(dbContext.Sections, cp => cp.Newid, s => s.Id, (cp, s) => s).Where(s => s.OfflineTitleMediafileId != null)];
            sections.ForEach(n => {
                n.TitleMediafileId = GetMappedId(Tables.Mediafiles, mapKey, n.OfflineTitleMediafileId);
            });
            dbContext.Sections.UpdateRange(sections);
            List<Sharedresource> resources = [.. dbContext.Copyprojects.Where(c => c.Sourcetable == Tables.SharedResources && c.Newprojid == mapKey)
                                        .Join(dbContext.Sharedresources, cp => cp.Newid, s => s.Id, (cp, s) => s).Where(s => s.OfflineTitleMediafileId != null)];
            resources.ForEach(n => {
                n.TitleMediafileId = GetMappedId(Tables.Mediafiles, mapKey, n.OfflineTitleMediafileId);
            });
            dbContext.Sharedresources.UpdateRange(resources);

            List<Passage> psgs =  [.. dbContext.Copyprojects.Where(c => c.Sourcetable == Tables.Passages && c.Newprojid == mapKey)
                                        .Join(dbContext.Passages, cp => cp.Newid, m => m.Id, (cp, m) => m).Where(m => m.OfflineSharedResourceId != null)];
            psgs.ForEach(p => p.SharedResourceId = GetMappedId(Tables.SharedResources, mapKey, p.OfflineSharedResourceId));
            dbContext.Passages.UpdateRange(psgs);

            //I may not need to do this because it's handled in UpdateOfflineIds...
            //internalization resources from general resource...
            List<Mediafile> mediafiles = [.. dbContext.Copyprojects.Where(c => c.Sourcetable == Tables.Mediafiles && c.Newprojid == mapKey)
                                        .Join(dbContext.Mediafiles, cp => cp.Newid, m => m.Id, (cp, m) => m).Where(m => m.OfflineSourceMediaId != null)];
            mediafiles.ForEach(m => m.SourceMediaId = GetMappedId(Tables.Mediafiles, mapKey, m.OfflineSourceMediaId));

            dbContext.Mediafiles.UpdateRange(mediafiles);

            dbContext.SaveChanges();
            UpdateOfflineIds();
        }

        public async Task<Fileresponse> ProcessImportCopyFileAsync(
                ZipArchive archive,
                int existingOrgId,
                string sFile,
                int start,
                string? myMapKey)
        {
            //can't wait for a new project id since we have to process 20 entries before then
            //use
            //give myself 20 seconds to get as much as I can...
            DateTime? dtBail = DateTime.Now.AddSeconds(20);
            User currentuser = CurrentUser() ?? new User();
            string name = "";
            try
            {
                HttpContext?.SetFP("import");

                Project? sourceproject = GetFileProject(archive); //don't pass in the mapKey here.  we don't want the org mapped yet.

                string mapKey = myMapKey ?? $"{sourceproject?.OfflineId}{DateTime.Now.Ticks}";

                bool newOrg = (existingOrgId == 0);
                bool sameOrg = !newOrg && sourceproject?.OrganizationId == existingOrgId;

                if (start == 0)
                {
                    DateTime? sourceDate = CheckSILTranscriber(archive);
                    if (sourceDate == null)
                        return ErrorResponse("SILTranscriber not present", sFile);
                    Organization? fileorg = ReadFileOrganization(archive);
                    if (existingOrgId == 0 && fileorg == null)
                        return ErrorResponse("No organization found in file", sFile);
                    if (sourceproject == null)
                        return ErrorResponse("No project found in file", sFile);
                }

                IJsonApiOptions options = new JsonApiOptions();
                bool complete = true;
                int orgid = existingOrgId > 0 ? existingOrgId : mapKey != "" ? GetSingleId(Tables.Organizations, mapKey) : 0;
                Project? project = null;
                Plan? plan = null;
                string status = "";
                int entryNum = start;
                foreach (ZipArchiveEntry entry in archive.Entries
                    .Where(e => e.FullName.StartsWith("data"))
                    .OrderBy(e => e.Name)
                    .Skip(start))
                {
                    entryNum++;
                    name = Path.GetFileNameWithoutExtension(entry.Name[2..]);
                    //Logger.LogCritical("{n} {cl} {l}", entry.FullName, entry.CompressedLength, entry.Length);
                    string? json = new StreamReader(entry.Open()).ReadToEnd();
                    Document? doc = JsonSerializer.Deserialize<Document>(
                            json,
                            options.SerializerReadOptions
                        );
                    IList<ResourceObject>? lst = doc?.Data.ManyValue;
                    if (lst != null)
                        lst = [.. lst.DistinctBy(ro => ro.Id ?? "")];
                    if (doc == null || lst == null)
                        continue;
                    status = $"{entryNum}/{archive.Entries.Count}";
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
                            SaveMap(MapActivityStates(acs), name, mapKey);
                            break;
                        case Tables.Integrations:
                            List<Integration> records = [];
                            foreach (ResourceObject ro in lst)
                                records.Add(ResourceObjectToResource(ro, new Integration(), mapKey));
                            SaveMap(MapIntegrations(records), name, mapKey);
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
                            foreach (string email in UsersToInvite)
                            {
                                InviteUserToOrg(orgid, email);
                            }
                            dbContext.SaveChanges();

                            break;
                        case Tables.PassageTypes:
                            List<Passagetype> pts = [];
                            foreach (ResourceObject ro in lst)
                                pts.Add(ResourceObjectToResource(ro, new Passagetype(), mapKey));
                            SaveMap(MapPassageTypes(pts), name, mapKey);
                            break;
                        case Tables.PlanTypes:
                            List <Plantype> plts = [];
                            foreach (ResourceObject ro in lst)
                                plts.Add(ResourceObjectToResource(ro, new Plantype(), mapKey));
                            SaveMap(MapPlanTypes(plts), name, mapKey);
                            break;
                        case Tables.ProjectTypes:
                            List <Projecttype> prts = [];
                            foreach (ResourceObject ro in lst)
                                prts.Add(ResourceObjectToResource(ro, new Projecttype(), mapKey));
                            SaveMap(MapProjectTypes(prts), name, mapKey);
                            break;
                        case Tables.Roles:
                            List <Role> roles = [];
                            foreach (ResourceObject ro in lst)
                                roles.Add(ResourceObjectToResource(ro, new Role(), mapKey));
                            SaveMap(MapRoles(roles), name, mapKey);
                            break;
                        case Tables.WorkflowSteps:
                            List <Workflowstep> workflowsteps = [];
                            foreach (ResourceObject ro in lst)
                                workflowsteps.Add(ResourceObjectToResource(ro, new Workflowstep(), mapKey));
                            SaveMap(MapWorkflowsteps(workflowsteps), name, mapKey);
                            break;
                        case Tables.ArtifactCategorys:
                            List<Artifactcategory> ac = [];
                            if (orgid == 0)
                                throw new Exception("No Org in ArtifactCategory");
                            foreach (ResourceObject ro in lst)
                                ac.Add(ResourceObjectToResource(ro, new Artifactcategory(), mapKey));
                            SaveMap(CopyArtifactCategorys([.. ac.Where(s => s.OrganizationId == orgid || s.OrganizationId is null)], orgid), name, mapKey);
                            break;

                        case Tables.ArtifactTypes:
                            SaveMap(MapArtifactTypes(lst, mapKey), name, mapKey);
                            break;

                        case Tables.Groups:
                            if (orgid == 0)
                                throw new Exception("No Org in Groups");
                            //get it from the org
                            int grpId = dbContext.Groups.FirstOrDefault(g => g.OwnerId == orgid && !g.Archived)?.Id ?? 0;
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
                            owlst = [.. owlst.Where(s => s.OrganizationId == orgid)];
                            SaveMap(CopyOrgworkflowsteps(owlst, orgid), name, mapKey);
                            break;

                        case Tables.GroupMemberships:
                            //ignore - we already added all our users to the group
                            continue;

                        case Tables.Projects:
                            Project p = ResourceObjectToResource(lst.First(), new Project(), mapKey);
                            project = CreateNewProject(p, true, orgid, currentuser);
                            SaveId(Tables.Projects, p.OfflineId, project.Id, mapKey);
                            break;


                        case Tables.IntellectualPropertys:
                            //copy but change the organization to current org
                            List<Intellectualproperty> iplst = [];
                            foreach (ResourceObject ro in lst)
                                iplst.Add(ResourceObjectToResource(ro, new Intellectualproperty(), mapKey));
                            CopyIP(iplst);
                            break;


                        case Tables.Plans:
                            if (project is null)
                            {
                                int id = GetSingleId(Tables.Projects, mapKey);
                                project = dbContext.Projects.Find(id);
                            }
                            List<Plan> planlst = [.. lst.Select(ro => ResourceObjectToResource(ro, new Plan(), mapKey))];
                            Plan? sourceplan = planlst.Find(p => p.Project?.Id == project?.Id) ?? throw new Exception("Plan not found for project");
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
                            IdMap smap = GetMap(name, mapKey);
                            int lstCount = lst.Count;
                            lst = [.. lst.Where(ro => !smap.ContainsKey(ro.Id ?? ""))];

                            while (smap.Count < lst.Count && DateTime.Now < dtBail)
                            {
                                IEnumerable<ResourceObject> tmpchunk = lst.Skip(smap.Count).Take(DataChunkSize);
                                List<Section> slst = [.. tmpchunk
                                    .Select(ro => ResourceObjectToResource(ro, new Section(), mapKey))];
                                IdMap newIds = CopySections(slst, plan?.Id ?? 0, dtBail);
                                SaveMap(newIds, name, mapKey);
                                smap = smap.Union(newIds).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (smap.Count < lst.Count)
                            {
                                status = $"{name} {smap.Count}/{lstCount}";
                                entryNum--; //we must have bailed out because of time, so continue to start here.
                            }
                            break;

                        case Tables.Passages:
                            List<Passage> plst = [];
                            IdMap pmap = GetMap(name, mapKey);
                            while (pmap.Count < lst.Count && DateTime.Now < dtBail)
                            {
                                IEnumerable<ResourceObject> tmpchunk = lst.Skip(pmap.Count).Take(DataChunkSize);
                                foreach (ResourceObject ro in tmpchunk)
                                {
                                    Passage psg = ResourceObjectToResource(ro, new Passage(), mapKey);
                                    if (psg.Section != null) //supporting passages won't be imported
                                        plst.Add(psg);
                                    else
                                        pmap.Add(psg.OfflineId, -1);
                                }
                                IdMap newIds = CopyPassages(plst, mapKey, dtBail);
                                SaveMap(newIds, name, mapKey);
                                pmap = pmap.Union(newIds).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (pmap.Count < lst.Count)
                            {
                                status = $"{name} {pmap.Count}/{lst.Count}";
                                entryNum--; //we must have bailed out because of time, so continue to start here.
                            }
                            break;

                        case Tables.SectionResources:
                            IdMap srmap = GetMap(name, mapKey);
                            while (srmap.Count < lst.Count && DateTime.Now < dtBail)
                            {
                                IEnumerable<ResourceObject> tmpchunk = lst.Skip(srmap.Count).Take(DataChunkSize);
                                List<Sectionresource> srlst = [.. tmpchunk
                                    .Select(ro => ResourceObjectToResource(ro, new Sectionresource(), mapKey))];

                                IdMap newIds = CopySectionResources(srlst, orgid, GetSingleId(Tables.Projects, mapKey), dtBail);
                                SaveMap(newIds, name, mapKey);
                                srmap = srmap.Union(newIds).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (srmap.Count < lst.Count)
                            {
                                status = $"{name} {srmap.Count}/{lst.Count}";
                                entryNum--; //we must have bailed out because of time, so continue to start here.
                            }
                            break;

                        case Tables.SectionResourceUsers:
                            //don't copy user completion info
                            break;

                        case Tables.Mediafiles:
                            if (plan is null)
                            {
                                int id = GetSingleId(Tables.Plans, mapKey);
                                plan = dbContext.Plans.Find(id);
                            }
                            IdMap mfMap = GetMediafileMap(mapKey);
                            int totalCount = lst.Count;
                            while (mfMap.Count < totalCount && DateTime.Now < dtBail)
                            {
                                int skip = mfMap.Count;
                                List<Mediafile> chunk = [.. lst.Skip(skip).Take(MediafileChunkSize)
                                    .Select(ro => ResourceObjectToResource(ro, new Mediafile(), mapKey))];
                                mfMap = await CopyMediafilesAsync(chunk, plan, mapKey, dtBail, archive);
                            }
                            MediafileMap = mfMap;
                            if (mfMap.Count < totalCount)
                            {
                                status = $"{name} {mfMap.Count}/{lst.Count}";
                                entryNum--; //we must have bailed out because of time, so continue to start here.
                            }
                            break;

                        case Tables.Discussions:
                            IdMap dmap = GetMap(name, mapKey);
                            while (dmap.Count < lst.Count && DateTime.Now < dtBail)
                            {
                                IEnumerable<ResourceObject> tmpchunk = lst.Skip(dmap.Count).Take(DataChunkSize);
                                List<Discussion> dlst = [.. tmpchunk
                                    .Select(ro => ResourceObjectToResource(ro, new Discussion(), mapKey))];
                                IdMap newIds = CopyDiscussions(dlst,  dtBail);
                                SaveMap(newIds, name, mapKey);
                                dmap = dmap.Union(newIds).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (dmap.Count < lst.Count)
                            {
                                status = $"{name} {dmap.Count}/{lst.Count}";
                                entryNum--; //we must have bailed out because of time, so continue to start here.
                            }
                            break;

                        case Tables.Comments:
                            IdMap cmap = GetMap(name, mapKey);
                            while (cmap.Count < lst.Count && DateTime.Now < dtBail)
                            {
                                IEnumerable<ResourceObject> tmpchunk = lst.Skip(cmap.Count).Take(DataChunkSize);
                                List<Comment> clst =[.. tmpchunk
                                    .Select(ro => ResourceObjectToResource(ro, new Comment(), mapKey))];
                                IdMap newIds = CopyComments(clst,  dtBail);
                                SaveMap(newIds, name, mapKey);
                                cmap = cmap.Union(newIds).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (cmap.Count < lst.Count)
                            {
                                status = $"{name} {cmap.Count}/{lst.Count}";
                                entryNum--; //we must have bailed out because of time, so continue to start here.
                            }
                            break;

                        case Tables.SharedResources:
                            List<Sharedresource> shrlst = [];
                            IdMap shrmap = GetMap(name, mapKey);
                            while (shrmap.Count < lst.Count && DateTime.Now < dtBail)
                            {
                                IEnumerable<ResourceObject> tmpchunk = lst.Skip(shrmap.Count).Take(DataChunkSize);
                                foreach (ResourceObject ro in tmpchunk)
                                {
                                    Sharedresource sr = ResourceObjectToResource(ro, new Sharedresource(), mapKey);
                                    if (sr.Passage == null) //a shared resource that we didn't import the passage
                                    {
                                        //find an owner
                                        Passage? psg = dbContext.Passages.Where(p => p.OfflineSharedResourceId == ro.Id).FirstOrDefault();
                                        if (psg != null)
                                        {
                                            string psgid = ro.Id?.ToString()??"";
                                            List<Mediafile> media = [.. dbContext.Mediafiles.Where(m => m.OfflinePassageId == psgid)];
                                            media.ForEach(m => m.PassageId = psg.Id);
                                            sr.PassageId = psg.Id;
                                            psg.OfflineSharedResourceId = null;
                                            psg.SharedResourceId = null; //I'm the owner now
                                            List<Mediafile> internalizemedia = [.. dbContext.Mediafiles.Where(m => m.OfflineResourcePassageId == psgid)];
                                            internalizemedia.ForEach(m => m.ResourcePassageId = psg.Id);
                                            dbContext.SaveChanges();

                                        }
                                    }
                                    shrlst.Add(sr);
                                }
                                IdMap newIds = CopySharedResources(shrlst, shrmap, dtBail);
                                SaveMap(newIds, name, mapKey);
                                shrmap = shrmap.Union(newIds).ToDictionary(k => k.Key, v => v.Value);
                            }
                            if (shrmap.Count < lst.Count)
                            {
                                status = $"{name} {shrmap.Count}/{lst.Count}";
                                entryNum--; //we must have bailed out because of time, so continue to start here.
                            }
                            break;

                        case Tables.SharedResourceReferences:
                            List<Sharedresourcereference> shrrlst = [];
                            foreach (ResourceObject ro in lst)
                            {
                                Sharedresourcereference srr = ResourceObjectToResource(ro, new Sharedresourcereference(), mapKey);
                                if (srr.SharedResourceId > 0)
                                    shrrlst.Add(srr);
                            }
                            CopySharedResourceReferences(shrrlst);
                            break;

                        case Tables.OrgKeyTerms:
                            List<Orgkeyterm> oktlst = [];
                            foreach (ResourceObject ro in lst)
                                oktlst.Add(ResourceObjectToResource(ro, new Orgkeyterm(), mapKey));
                            SaveMap(CopyOrgKeyTerms(oktlst, orgid), name, mapKey);
                            break;

                        case Tables.OrgKeyTermTargets:
                            List<Orgkeytermtarget> okttlst = [];
                            foreach (ResourceObject ro in lst)
                                okttlst.Add(ResourceObjectToResource(ro, new Orgkeytermtarget(), mapKey));
                            CopyOrgkeytermtargets(okttlst);
                            break;

                        case Tables.OrgKeyTermReferences:
                            List<Orgkeytermreference> oktrlst = [..lst.Select(ro => ResourceObjectToResource(ro, new Orgkeytermreference(), mapKey))];
                            CopyOrgkeytermreferences(oktrlst);
                            break;

                        case Tables.Graphics:
                            IdMap gmap = GetMap(name, mapKey);
                            while (gmap.Count < lst.Count && DateTime.Now < dtBail)
                            {
                                IEnumerable<ResourceObject> tmpchunk = lst.Skip(gmap.Count).Take(DataChunkSize);
                                List<Graphic> graphicList =[.. tmpchunk
                                    .Select(ro => ResourceObjectToResource(ro, new Graphic(), mapKey))];
                                gmap = CopyGraphics(graphicList, mapKey, gmap, dtBail);
                            }
                            if (gmap.Count < lst.Count)
                            {
                                status = $"{name} {gmap.Count}/{lst.Count}";
                                entryNum--; //we must have bailed out because of time, so continue to start here.
                            }
                            break;

                        case Tables.OrganizationSchemes:
                            List<Organizationscheme> osList = [..lst.Select(ro => ResourceObjectToResource(ro, new Organizationscheme(), mapKey))];
                            SaveMap(CopyOrgSchemes(osList, orgid), name, mapKey);
                            break;

                        case Tables.OrganizationSchemeSteps:
                            List<Organizationschemestep> ossList = [..lst.Select(ro => ResourceObjectToResource(ro, new Organizationschemestep(), mapKey))];
                            CopyOrgSchemeSteps(ossList);
                            break;
                    }
                    if (DateTime.Now >= dtBail)
                    {
                        Logger.LogCritical("Bailing on import of {n} at entry {e} because time is up. {d}", sourceproject?.Name, entry.FullName, DateTime.Now);
                        complete = false;
                        break;
                    }
                }
                _ = dbContext.SaveChanges();
                string orgName = "";
                string projName = "";
                if (complete)
                {
                    FixEarlyIds(mapKey);
                    orgName = dbContext.Organizations.Find(orgid)?.Name ?? orgid.ToString();
                    if (project is null)
                    {
                        int id = GetSingleId(Tables.Projects, mapKey);
                        project = dbContext.Projects.Find(id);
                    }
                    projName = project?.Name ?? "";
                }

#pragma warning restore CS8604 // Possible null reference argument.

                return new Fileresponse()
                {
                    Id = complete ? orgid : entryNum,
                    Message = complete ? string.Format("{0} {1}", orgName, projName) : status,
                    FileURL = mapKey,
                    Status = complete ? HttpStatusCode.OK : HttpStatusCode.PartialContent,
                    Startindex = complete ? "" : entryNum.ToString(),
                    ContentType = "application/ptf",
                };
            }
            catch (Exception ex)
            {
                return ErrorResponse(
                    name + " " + ex.Message
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
        //DEPRECATED!!
        private async Task<Fileresponse> ProcessImportDeprecatedCopyFileAsync(
                ZipArchive archive,
                bool neworg,
                string sFile)
        {

            DateTime? sourceDate = CheckSILTranscriber(archive);
            if (sourceDate == null)
                return ErrorResponse("SILTranscriber not present", sFile);

            int orgId = 0;

            //check project
            Project? sourceproject = GetFileProject(archive);
            bool sameOrg = !neworg && sourceproject?.Id > 0;
            if (sameOrg)
            {
                int orgid = sourceproject?.OrganizationId??0;
                Organization? org = dbContext.Organizations.FirstOrDefault(o => o.Id == orgid);
                if (org == null && !neworg)
                {
                    Organization? fileorg = ReadFileOrganization(archive);
                    User currentuser = CurrentUser() ?? new User();
                    string orgName = fileorg?.Name ?? "Unknown";
                    org = dbContext.Organizations.FirstOrDefault(o => o.Name == orgName && o.OwnerId == currentuser.Id && !o.Archived);
                }
                orgId = org?.Id ?? 0;
            }
            //TODO add start and newProjId parameters to allow resuming
            return await ProcessImportCopyFileAsync(archive, orgId, sFile, 0, null);
        }
    }
    #endregion Copy
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons

}
