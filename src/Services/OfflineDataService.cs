using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System.IO.Compression;
using System.Collections;
using static SIL.Transcriber.Utility.ResourceHelpers;
using System.Net;
using SIL.Transcriber.Repositories;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Utility.Extensions;
using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Serialization;
using JsonApiDotNetCore.Serialization.Response;

namespace SIL.Transcriber.Services
{
    public class OfflineDataService : IOfflineDataService
    {
        protected readonly AppDbContext dbContext;
        protected readonly MediafileService mediaService;
        protected CurrentUserRepository CurrentUserRepository { get; }

        readonly private IS3Service _S3service;
        const string ImportFolder = "imports";
        const string ExportFolder = "exports";

        protected ILogger<OfflineDataService> Logger { get; set; }

        readonly IResourceGraph _resourceGraph;
        readonly IResourceDefinitionAccessor _resourceDefinitionAccessor;
        readonly IMetaBuilder _metaBuilder;
        readonly IJsonApiOptions _options;

        public OfflineDataService(
            AppDbContextResolver contextResolver,
            MediafileService MediaService,
            CurrentUserRepository currentUserRepository,
            IS3Service service,
            ILoggerFactory loggerFactory,
            IResourceGraph resourceGraph,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            IMetaBuilder metaBuilder,
            IJsonApiOptions options
        )
        {
            dbContext = (AppDbContext)contextResolver.GetContext();
            mediaService = MediaService;
            CurrentUserRepository = currentUserRepository;
            _S3service = service;
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
            using (StreamWriter sw = new StreamWriter(entry.Open()))
            {
                sw.WriteLine(contents);
            }
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
            return SerializerHelpers.ResourceListToJson<TResource>(
                resources,
                _resourceGraph,
                _options,
                _resourceDefinitionAccessor,
                _metaBuilder
            );
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
                    "media/" + Path.GetFileNameWithoutExtension(name) + ".eaf",
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
            if (s != null)
                return AddStreamEntry(zipArchive, s, dir, newName);
            return false;
        }

        private static async Task<Stream?> GetStreamFromUrlAsync(string url)
        {
            byte[]? imageData = null;
            try
            {
                using HttpClient client = new HttpClient();
                using HttpResponseMessage response = await client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead
                );
                using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();
                string fileToWriteTo = Path.GetTempFileName();
                using (Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create))
                {
                    await streamToReadFrom.CopyToAsync(streamToWriteTo);
                }
            }
            catch
            {
                return null;
            }
            return imageData == null ? null : new MemoryStream(imageData);
        }

        private static void AddOrgLogos(ZipArchive zipArchive, List<Organization> orgs)
        {
            orgs.ForEach(o =>
            {
                if (!string.IsNullOrEmpty(o.LogoUrl))
                {
                    AddStreamEntry(zipArchive, o.LogoUrl, "logos/", o.Slug + ".png");
                    //    o.LogoUrl = "logos/" + o.Slug + ".png";
                    //else
                    //    o.LogoUrl = null;
                }
            });
        }

        private static void AddUserAvatars(ZipArchive zipArchive, List<User> users)
        {
            users.ForEach(u =>
            {
                if (!string.IsNullOrEmpty(u.AvatarUrl))
                {
                    AddStreamEntry(
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
            List<Mediafile> media
        )
        {
            if (DateTime.Now > dtBail)
                return false;
            if (completed <= check)
            {
                foreach (Mediafile m in media)
                    AddEafEntry(zipArchive, m.S3File ?? "", mediaService.EAF(m));
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
                int err = "abc".IndexOf("d");
                //extract the url
                int start = css.IndexOf("url('") + 4;
                if (start != 3)
                {
                    int end = css.IndexOf("')", start);
                    url = css[start..end];
                    string fontfile = url[(url.LastIndexOf("/") + 1)..];
                    url = bucket + fontfile;
                    AddStreamEntry(zipArchive, url, "fonts/", fontfile);
                    css = css.Substring(0, start + 1) + fontfile + css[end..];
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
            using (HttpClient client = new HttpClient())
            {
                foreach (string f in fonts)
                {
                    string cssfile = f.Split(',')[0].Replace(" ", "") + ".css";
                    AddFont(zipArchive, client, cssfile);
                }
            }
        }

        /// <summary>
        /// Strip illegal chars and reserved words from a candidate filename (should not include the directory path)
        /// </summary>
        /// <remarks>
        /// http://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
        /// </remarks>
        public static string CoerceValidFileName(string filename)
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
            Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail)
                return false;
            if (completed <= check)
            {
                AddJsonEntry(zipArchive, table, list, sort);
                completed++;
            }
            return true;
        }

        private Fileresponse CheckProgress(int projectid, string fileName, int lastAdd, string ext)
        {
            int startNext;
            string err = "";
            try
            {
                Stream ms = OpenFile(fileName + ".sss");
                StreamReader reader = new StreamReader(ms);
                string data = reader.ReadToEnd();
                if (data.IndexOf("|") > 0)
                {
                    err = data[(data.IndexOf("|") + 1)..];
                    data = data.Substring(0, data.IndexOf("|"));
                }
                if (!int.TryParse(data, out startNext))
                    startNext = 0;
            }
            catch
            {
                //it's not there yet...
                Logger.LogInformation("status file not available");
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
            string contentType = "application/zip";
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

        private Stream OpenFile(string fileName)
        {
            S3Response s3response = _S3service.ReadObjectDataAsync(fileName, ExportFolder).Result;
            if (s3response.FileStream == null)
                throw (new Exception("Export in progress " + fileName + "not found."));
            return s3response.FileStream;
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
                ms = OpenFile(fileName + ext);
            }
            return ms;
        }

        private Fileresponse WriteMemoryStream(
            Stream ms,
            string fileName,
            int startNext,
            int lastAdd,
            string ext
        )
        {
            S3Response s3response;
            string contentType = "application/zip";
            ms.Position = 0;
            fileName += ext + (startNext == lastAdd + 1 ? ".tmp" : ""); //tmp signals the trigger to add mediafiles
            s3response = _S3service
                .UploadFileAsync(ms, true, contentType, fileName, ExportFolder)
                .Result;
            if (s3response.Status == HttpStatusCode.OK)
            {
                return new Fileresponse()
                {
                    Message = fileName,
                    FileURL = "",
                    Status = HttpStatusCode.PartialContent,
                    ContentType = contentType,
                    Id = startNext,
                };
            }
            else
            {
                throw new Exception(s3response.Message);
            }
        }

        private void AddBurritoMeta(
            ZipArchive zipArchive,
            Project project,
            List<Mediafile> mediafiles
        )
        {
            Dictionary<string, string> mimeMap = new Dictionary<string, string>
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
            Dictionary<string, List<string>> scopes = new Dictionary<string, List<string>>();
            List<string> formats = new List<string>();

            dynamic? root = Newtonsoft.Json.JsonConvert.DeserializeObject(metastr);
            if (root == null)
                throw new Exception("Bad Meta" + metastr);
            root.meta.version = "0.3.1";
            root.meta.meta.category = "source";
            root.meta.generator.softwareName = "SIL Transcriber";
            root.meta.generator.softwareVersion =
                dbContext.Currentversions.FirstOrDefault()?.DesktopVersion ?? "unknown";
            root.meta.generator.userName = CurrentUser()?.Name ?? "unknown";
            root.meta.defaultLanguage = project.Language;
            root.meta.dateCreated = DateTime.Now.ToString("o");
            root.identification.name.en = project.Name;
            root.identification.description.en = project.Description;
            root.languages[0].tag = project.Language;
            root.languages[0].name.en = project.LanguageName;

            mediafiles.ForEach(m =>
            {
                //get stored book and ref out of audioquality
                string[] split = (m.AudioQuality ?? "|").Split("|");
                string book = split[0];
                string reference = split[1];
                if (!scopes.ContainsKey(book))
                    scopes.Add(book, new List<string>());
                scopes[book].Add(reference);
                string ext = Path.GetExtension(m.AudioUrl ?? "").TrimStart('.');
                if (!formats.Contains(ext))
                    formats.Add(ext);
                root.ingredients[m.AudioUrl] = new JObject();
                if (mimeMap.ContainsKey(ext))
                    root.ingredients[m.AudioUrl].mimeType = mimeMap[ext];
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
            if (passage == null || language == null)
                return "";

            return "release/audio/"
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

        private List<Mediafile> AddBurritoMedia(
            ZipArchive zipArchive,
            Project project,
            List<Mediafile> mediafiles
        )
        {
            mediafiles.ForEach(m =>
            {
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
            AddJsonEntry(zipArchive, "attachedmediafiles", mediafiles, 'Z');
            return mediafiles;
        }

        private void AddAttachedMedia(ZipArchive zipArchive, List<Mediafile> mediafiles)
        {
            mediafiles.ForEach(m =>
            {
                //S3File has just the filename
                //AudioUrl has the signed GetUrl which has the path + filename as url (so spaces changed etc) + signed stuff
                //change the audioUrl to have the offline path + filename
                //change the s3File to have the onlinepath + filename
                m.AudioUrl = "media/" + m.S3File;
                m.S3File = mediaService.DirectoryName(m) + "/" + m.S3File;
            });
            AddJsonEntry(zipArchive, "attachedmediafiles", mediafiles, 'Z');
        }

        public Fileresponse ExportProjectAudio(
            int projectid,
            string artifactType,
            string idList,
            int start
        )
        {
            int LAST_ADD = 0;
            const string ext = ".audio";
            int startNext = start;

            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == projectid);
            Project project = projects.First();
            string fileName = string.Format(
                "Audio{0}_{1}_{2}",
                CoerceValidFileName(project.Name + artifactType),
                project.Id.ToString(),
                CurrentUser()?.Id
            );
            if (start > LAST_ADD)
                return CheckProgress(projectid, fileName + ext, LAST_ADD, ext);

            Stream ms = GetMemoryStream(start, fileName, ext);
            using (ZipArchive zipArchive = new ZipArchive(ms, ZipArchiveMode.Update, true))
            {
                if (start == 0)
                {
                    DateTime exported = AddCheckEntry(
                        zipArchive,
                        dbContext.Currentversions.FirstOrDefault()?.SchemaVersion ?? 4
                    );
                    List<Mediafile> mediafiles = dbContext.Mediafiles
                        .Where(x => idList.Contains("," + x.Id.ToString() + ","))
                        .ToList();
                    AddJsonEntry(zipArchive, "mediafiles", mediafiles, 'H');
                    AddMediaEaf(
                        0,
                        DateTime.Now.AddSeconds(15),
                        ref startNext,
                        zipArchive,
                        mediafiles
                    );
                    AddAttachedMedia(zipArchive, mediafiles);
                    startNext = 1;
                }
            }
            return WriteMemoryStream(ms, fileName, startNext, LAST_ADD, ext);
        }

        public Fileresponse ExportBurrito(int projectid, string idList, int start)
        {
            int LAST_ADD = 0;
            const string ext = ".burrito";
            int startNext = start;

            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == projectid);
            Project project = projects.First();
            string fileName = string.Format(
                "{0}_{1}_{2}",
                CoerceValidFileName(project.Name),
                project.Id.ToString(),
                CurrentUser()?.Id
            );

            if (start > LAST_ADD)
                return CheckProgress(projectid, fileName + ext, LAST_ADD, ext);

            Stream ms = GetMemoryStream(start, fileName, ext);
            using (ZipArchive zipArchive = new ZipArchive(ms, ZipArchiveMode.Update, true))
            {
                if (start == 0)
                {
                    List<Mediafile> mediaList = dbContext.Mediafiles
                        .Where(x => idList.Contains("," + x.Id.ToString() + ","))
                        .ToList();
                    mediaList = AddBurritoMedia(zipArchive, project, mediaList);
                    AddBurritoMeta(zipArchive, project, mediaList);
                    startNext = 1;
                }
            }
            return WriteMemoryStream(ms, fileName, startNext, LAST_ADD, ext);
        }

        public Fileresponse ExportProjectPTF(int projectid, int start)
        {
            const int LAST_ADD = 15;
            const string ext = ".ptf";
            int startNext = start;
            //give myself 15 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(15);

            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == projectid);
            Project project = projects.First();
            string fileName = string.Format(
                "Transcriber{0}_{1}_{2}",
                CoerceValidFileName(project.Name),
                project.Id.ToString(),
                CurrentUser()?.Id
            );

            if (start > LAST_ADD)
                return CheckProgress(projectid, fileName + ext, LAST_ADD, ext);

            Stream ms = GetMemoryStream(start, fileName, ext);
            using (ZipArchive zipArchive = new ZipArchive(ms, ZipArchiveMode.Update, true))
            {
                if (start == 0)
                {
                    Dictionary<string, string> fonts = new Dictionary<string, string>();
                    fonts.Add("Charis SIL", "");
                    DateTime exported = AddCheckEntry(
                        zipArchive,
                        dbContext.Currentversions.FirstOrDefault()?.SchemaVersion ?? 4
                    );
                    AddJsonEntry(
                        zipArchive,
                        "activitystates",
                        dbContext.Activitystates.ToList(),
                        'B'
                    );
                    AddJsonEntry(zipArchive, "integrations", dbContext.Integrations.ToList(), 'B');
                    AddJsonEntry(zipArchive, "plantypes", dbContext.Plantypes.ToList(), 'B');
                    AddJsonEntry(zipArchive, "projecttypes", dbContext.Projecttypes.ToList(), 'B');
                    AddJsonEntry(zipArchive, "roles", dbContext.Roles.ToList(), 'B');
                    AddJsonEntry(
                        zipArchive,
                        "workflowsteps",
                        dbContext.Workflowsteps.ToList(),
                        'B'
                    );

                    //org
                    IQueryable<Organization> orgs = dbContext.Organizations.Where(
                        o => o.Id == project.OrganizationId
                    );
                    List<Organization> orgList = orgs.ToList();

                    AddOrgLogos(zipArchive, orgList);
                    AddJsonEntry(zipArchive, "organizations", orgList, 'B');

                    //groups
                    IQueryable<Group> groups = dbContext.Groups.Join(
                        projects,
                        g => g.Id,
                        p => p.GroupId,
                        (g, p) => g
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
                    List<User> userList = users.ToList();
                    AddUserAvatars(zipArchive, userList);

                    AddJsonEntry(
                        zipArchive,
                        "groups",
                        groups.Where(g => !g.Archived).ToList(),
                        'C'
                    );
                    //groupmemberships
                    AddJsonEntry(zipArchive, "groupmemberships", gms, 'D');
                    AddJsonEntry(zipArchive, "users", userList, 'A');

                    //organizationmemberships
                    IEnumerable<Organizationmembership> orgmems = users
                        .Join(
                            dbContext.Organizationmemberships,
                            u => u.Id,
                            om => om.UserId,
                            (u, om) => om
                        )
                        .Where(om => om.OrganizationId == project.OrganizationId && !om.Archived);
                    AddJsonEntry(zipArchive, "organizationmemberships", orgmems.ToList(), 'C');

                    //projects
                    AddJsonEntry(zipArchive, "projects", projects.ToList(), 'D');
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
                            'E'
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
                            'E'
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
                            'F'
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
                            'G'
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
                            'H'
                        )
                    )
                        break;
                    //mediafiles
                    IQueryable<Mediafile> mediafiles = plans
                        .Join(dbContext.Mediafiles, p => p.Id, m => m.PlanId, (p, m) => m)
                        .Where(x => !x.Archived);
                    IQueryable<Mediafile> attachedmediafiles = passages
                        .Join(dbContext.Mediafiles, p => p.Id, m => m.PassageId, (p, m) => m)
                        .Where(x => x.ResourcePassageId == null && !x.Archived);

                    //I need my mediafiles plus any shared resource mediafiles
                    IQueryable<Sectionresource> sectionresources = dbContext.Sectionresources
                        .Join(sections, r => r.SectionId, s => s.Id, (r, s) => r)
                        .Where(x => !x.Archived);

                    //get the mediafiles associated with section resources
                    IQueryable<Mediafile> resourcemediafiles = dbContext.Mediafiles
                        .Join(sectionresources, m => m.Id, r => r.MediafileId, (m, r) => m)
                        .Where(x => !x.Archived);

                    //now get any shared resource mediafiles associated with those mediafiles
                    IQueryable<Mediafile> sourcemediafiles = dbContext.Mediafiles
                        .Join(
                            resourcemediafiles,
                            m => m.PassageId,
                            r => r.ResourcePassageId,
                            (m, r) => m
                        )
                        .Where(x => x.ReadyToShare && !x.Archived);
                    //pick just the highest version media per passage
                    sourcemediafiles =
                        from m in sourcemediafiles
                        group m by m.PassageId into grp
                        select grp.OrderByDescending(m => m.VersionNumber).FirstOrDefault();

                    foreach (
                        Mediafile mf in resourcemediafiles.Where(m => m.ResourcePassageId != null)
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
                        dbContext.Mediafiles.Update(mf);
                    }
                    if (
                        !CheckAdd(
                            6,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "mediafiles",
                            mediafiles.OrderBy(m => m.Id).ToList(),
                            'H'
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
                                .Where(
                                    a =>
                                        (
                                            a.OrganizationId == null
                                            || a.OrganizationId == project.OrganizationId
                                        ) && !a.Archived
                                )
                                .ToList(),
                            'C'
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
                                .Where(
                                    a =>
                                        (
                                            a.OrganizationId == null
                                            || a.OrganizationId == project.OrganizationId
                                        ) && !a.Archived
                                )
                                .ToList(),
                            'C'
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
                            dbContext.Orgworkflowsteps
                                .Where(
                                    a => (a.OrganizationId == project.OrganizationId) && !a.Archived
                                )
                                .ToList(),
                            'C'
                        )
                    )
                        break;
                    IQueryable<Discussion> discussions = dbContext.Discussions
                        .Join(attachedmediafiles, d => d.MediafileId, m => m.Id, (d, m) => d)
                        .Where(x => !x.Archived);
                    if (
                        !CheckAdd(
                            10,
                            dtBail,
                            ref startNext,
                            zipArchive,
                            "discussions",
                            discussions.ToList(),
                            'I'
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
                            'J'
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
                            'G'
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
                            'H'
                        )
                    )
                        break;
                    //Now I need the media list of just those files to download...
                    //pick just the highest version media per passage (vernacular only)
                    IQueryable<Mediafile> vernmediafiles =
                        from m in attachedmediafiles
                        where m.ArtifactTypeId == null
                        group m by m.PassageId into grp
                        select grp.OrderByDescending(m => m.VersionNumber).FirstOrDefault();
                    List<Mediafile> mediaList = vernmediafiles.ToList();
                    if (!AddMediaEaf(14, dtBail, ref startNext, zipArchive, mediaList))
                        break;
                    mediaList = mediaList.Concat(sourcemediafiles).ToList();
                    //this should get comments and uploaded resources - not accounting for edited comments for now...
                    mediaList = mediaList
                        .Concat(mediafiles.Where(m => m.ArtifactTypeId != null))
                        .ToList();
                    AddAttachedMedia(zipArchive, mediaList);
                    startNext++;
                } while (false);
            }

            return WriteMemoryStream(ms, fileName, startNext, LAST_ADD, ext);
        }

        public Fileresponse ImportFileURL(string sFile)
        {
            string extension = Path.GetExtension(sFile);
            string ContentType = "application/" + extension;
            // Project project = dbContext.Projects.Where(p => p.Id == id).First();
            string fileName = string.Format(
                "{0}_{1}.{2}",
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
            if (
                (online.EditorId != imported.EditorId && online.EditorId != null)
                || (online.TranscriberId != imported.TranscriberId && online.TranscriberId != null)
                || online.State != imported.State
            )
            {
                return ChangesReport("section", Serialize(online), Serialize(imported));
            }
            return "";
        }

        private string PassageChangesReport(Passage online, Passage imported)
        {
            if (online.StepComplete != imported.StepComplete)
            {
                return ChangesReport("passage", Serialize(online), Serialize(imported));
            }
            return "";
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
            if (
                online.ArtifactCategoryId != imported.ArtifactCategoryId
                || online.GroupId != imported.GroupId
                || online.Resolved != imported.Resolved
            )
            {
                return ChangesReport("discussion", Serialize(online), Serialize(imported));
            }
            return "";
        }

        private string CommentChangesReport(Comment online, Comment imported)
        {
            if (
                online.CommentText != imported.CommentText
                || online.MediafileId != imported.MediafileId
            )
            {
                return ChangesReport("comment", Serialize(online), Serialize(imported));
            }
            return "";
        }

        private string GrpMemChangesReport(Groupmembership online, Groupmembership imported)
        {
            if (online.FontSize != imported.FontSize)
            {
                return ChangesReport("groupmembership", Serialize(online), Serialize(imported));
            }
            return "";
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
                        ? fr.Message.Substring(1, fr.Message.Length - 2)
                        : fr.Message;
                    report.Add(msg);
                }
                else
                {
                    errors.Add(JsonSerializer.Serialize(fr));
                }
            }
            report.RemoveAll(s => s.Length == 0);
            errors.RemoveAll(s => s.Length == 0);
            if (errors.Count > 0)
                return ErrorResponse(
                    "{\"errors\": ["
                        + string.Join(",", errors)
                        + "], \"report\": ["
                        + string.Join(", ", report)
                        + "]}",
                    sFile
                );

            return new Fileresponse()
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

        private Fileresponse ProjectDeletedResponse(string msg, string sFile)
        {
            return ErrorResponse(msg, sFile, System.Net.HttpStatusCode.MovedPermanently);
        }

        private Fileresponse NotCurrentProjectResponse(string msg, string sFile)
        {
            return ErrorResponse(msg, sFile, System.Net.HttpStatusCode.NotAcceptable);
        }

        private Fileresponse ErrorResponse(
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

        private async Task CopyMediaFiles(ZipArchive archive, List<Mediafile> mediafiles)
        {
            foreach (Mediafile m in mediafiles)
            {
                if (m.Id == 0)
                {
                    await CopyMediaFile(m, archive);
                }
            }
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
            IQueryable<Comment> comments = dbContext.Comments.Where(
                c => c.OfflineMediafileId != null
            );
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
            );
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
            IQueryable<Discussion> discussions = dbContext.Discussions.Where(
                d => d.MediafileId == null && d.OfflineMediafileId != null
            );
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

            IQueryable<Mediafile> mediafiles = dbContext.Mediafiles.Where(
                c => c.SourceMediaId == null && c.SourceMediaOfflineId != null
            );
            foreach (Mediafile m in mediafiles)
            {
                Mediafile? sourcemedia = dbContext.Mediafiles
                    .Where(sm => m.OfflineId == m.SourceMediaOfflineId)
                    .FirstOrDefault();
                if (sourcemedia != null)
                {
                    m.SourceMediaId = sourcemedia.Id;
                    m.LastModifiedOrigin = "electron";
                    m.DateUpdated = DateTime.UtcNow;
                }
            }
            dbContext.Mediafiles.UpdateRange(mediafiles);
            dbContext.SaveChanges();
        }

        private int CompareMediafilesByArtifactTypeVersionDesc(Mediafile a, Mediafile b)
        {
            if (a == null)
            {
                if (b == null)
                    return 0;
                else
                    return -1;
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
                            return (int)(a.VersionNumber ?? 0 - b.VersionNumber ?? 0); //both vernacular so use version number
                        else
                            return -1;
                    }
                    else
                    {
                        if (b.IsVernacular)
                            return 1;
                        else //neither is a vernacular so should all be version 1
                            return 0;
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
            if (
                !existing.Archived && existing.Subject != importing.Subject
                || (
                    existing.MediafileId != importing.MediafileId
                    || existing.ArtifactCategoryId != importing.ArtifactCategoryId
                    || existing.Resolved != importing.Resolved
                    || existing.GroupId != importing.GroupId
                )
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
                existing.DateUpdated = DateTime.UtcNow;
                dbContext.Discussions.Update(existing);
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
            )
            {
                if (existing.DateUpdated > sourceDate)
                    report.Add(CommentChangesReport(existing, importing));
                existing.CommentText = importing.CommentText;
                existing.MediafileId = importing.MediafileId;
                existing.OfflineMediafileId = importing.OfflineMediafileId;
                existing.DateUpdated = DateTime.UtcNow;
                existing.Archived = false;
                dbContext.Comments.Update(existing);
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
                existing.RecordedbyUserId = importing.RecordedbyUserId;
                existing.Segments = importing.Segments;
                existing.SourceSegments = importing.SourceSegments;
                existing.SourceMediaOfflineId = importing.SourceMediaOfflineId;
                existing.Topic = importing.Topic;
                existing.Transcription = importing.Transcription;
                if (importing.Transcriptionstate != null) //from old desktop
                    existing.Transcriptionstate = importing.Transcriptionstate;
                existing.LastModifiedBy = importing.LastModifiedBy;
                existing.DateUpdated = DateTime.UtcNow;
                dbContext.Mediafiles.Update(existing);
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
                    if (myTypeAttribute != null && row.Value != null)
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
            return string.Concat(snake.Substring(0, 1).ToUpper(), snake.AsSpan(1));
        }

        private async Task<Fileresponse> ProcessImportFileAsync(
            ZipArchive archive,
            int projectid,
            string sFile
        )
        {
            IJsonApiOptions options = new JsonApiOptions();
            DateTime sourceDate;
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
            try
            {
                ZipArchiveEntry? sourceEntry = archive.GetEntry("SILTranscriber");
                if (sourceEntry == null)
                    return ErrorResponse("SILTranscriber not present", sFile);
                sourceDate = Convert.ToDateTime(new StreamReader(sourceEntry.Open()).ReadToEnd());
            }
            catch
            {
                return ErrorResponse("SILTranscriber not present", sFile);
            }
            //check project if provided
            Project? project;
            try
            {
                ZipArchiveEntry? projectsEntry = archive.GetEntry("data/D_projects.json");
                if (projectsEntry == null)
                    return ErrorResponse("Project data not present", sFile);
                string json = new StreamReader(projectsEntry.Open()).ReadToEnd();

                Document? doc = JsonSerializer.Deserialize<Document>(
                    json,
                    options.SerializerReadOptions
                );
                ResourceObject? fileproject = doc?.Data.SingleValue ?? (doc?.Data.ManyValue?[0]);

                if (fileproject == null)
                    return ErrorResponse("Project data not present", sFile);

                project = dbContext.Projects.Find(int.Parse(fileproject.Id ?? "0"));
                if (projectid > 0)
                {
                    if (project == null || projectid != project.Id)
                    {
                        return NotCurrentProjectResponse(
                            project?.Name ?? "project not found",
                            sFile
                        );
                    }
                    if (project.Archived)
                        return ProjectDeletedResponse(project.Name, sFile);
                }
                if (project == null)
                    return ProjectDeletedResponse(
                        fileproject.Attributes?["name"]?.ToString() ?? "project not found",
                        sFile
                    );
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
                    List<Mediafile>? mediafiles = null;
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
                                        user.DateUpdated = DateTime.UtcNow;
                                        /* TODO: figure out if the avatar needs uploading */
                                        dbContext.Users.Update(user);
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
                                        section.LastModifiedBy = s.LastModifiedBy;
                                        section.DateUpdated = DateTime.UtcNow;
                                        dbContext.Sections.Update(section);
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
                                        passage.LastModifiedBy = p.LastModifiedBy;
                                        passage.DateUpdated = DateTime.UtcNow;
                                        dbContext.Passages.Update(passage);
                                        Passagestatechange psc = new Passagestatechange
                                        {
                                            Comments = "Imported", //TODO Localize
                                            DateCreated = passage.DateUpdated,
                                            DateUpdated = passage.DateUpdated,
                                            LastModifiedBy = currentuser,
                                            PassageId = passage.Id,
                                            State = passage.State ?? "",
                                        };
                                        dbContext.Passagestatechanges.Add(psc);
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
                                            dbContext.Discussions.Add(
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
                                            dbContext.Comments.Add(
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
                                                    DateCreated = c.DateCreated,
                                                    DateUpdated = DateTime.UtcNow,
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
                                                        .Where(
                                                            p =>
                                                                p.PassageId == m.PassageId
                                                                && !p.Archived
                                                                && p.IsVernacular
                                                        )
                                                        .OrderByDescending(p => p.VersionNumber)
                                                        .FirstOrDefault();
                                                    if (mediafile != null)
                                                        m.VersionNumber =
                                                            mediafile.VersionNumber + 1;
                                                }
                                                passageVersions[(int)m.PassageId] =
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
                                            dbContext.Mediafiles.Add(
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
                                                    RecordedbyUserId = m.RecordedbyUserId,
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
                                        grpmem.DateUpdated = DateTime.UtcNow;
                                        dbContext.Groupmemberships.Update(grpmem);
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
                                        dbContext.Passagestatechanges.Where(
                                            c =>
                                                c.PassageId == psc.PassageId
                                                && c.DateCreated == psc.DateCreated
                                                && c.State == psc.State
                                        );
                                    if (!dups.Any())
                                    { /* if I send psc in directly, the id goes wonky...must be *something* different in the way it is initialized (tried setting id=0), so copy relevant info here */
                                        dbContext.Passagestatechanges.Add(
                                            new Passagestatechange
                                            {
                                                PassageId = psc.PassageId,
                                                State = psc.State,
                                                DateCreated = psc.DateCreated,
                                                Comments = psc.Comments,
                                                LastModifiedBy = psc.LastModifiedBy,
                                                DateUpdated = DateTime.UtcNow,
                                            }
                                        );
                                    }
                                    ;
                                }
                                ;
                                break;
                        }
                    }
                    int ret = await dbContext.SaveChangesNoTimestampAsync();

                    UpdateOfflineIds();
                }
                report.RemoveAll(s => s.Length == 0);

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
    }
}
