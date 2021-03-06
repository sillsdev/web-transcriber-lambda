﻿using JsonApiDotNetCore.Data;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System;
using JsonApiDotNetCore.Serialization;

using System.Net;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class OfflineDataService : IOfflineDataService
    {
        protected readonly AppDbContext dbContext;
        protected readonly IJsonApiSerializer jsonApiSerializer;
        protected readonly IJsonApiDeSerializer jsonApiDeSerializer;
        protected readonly MediafileService mediaService;
        protected CurrentUserRepository CurrentUserRepository { get; }

        private IS3Service _S3service;
        const string ImportFolder = "imports";
        const string ExportFolder = "exports";
        const string ContentType = "application/ptf";
        protected ILogger<OfflineDataService> Logger { get; set; }

        public OfflineDataService(AppDbContextResolver contextResolver,
                IJsonApiSerializer jsonSer,
                IJsonApiDeSerializer jsonDeser,
                MediafileService MediaService,
                CurrentUserRepository currentUserRepository,
                IS3Service service,
                ILoggerFactory loggerFactory)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            jsonApiSerializer = jsonSer;
            jsonApiDeSerializer = jsonDeser;
            mediaService = MediaService;
            CurrentUserRepository = currentUserRepository;
            _S3service = service;
            this.Logger = loggerFactory.CreateLogger<OfflineDataService>();
        }

        private User CurrentUser() { return CurrentUserRepository.GetCurrentUser().Result; }

        private void WriteEntry(ZipArchiveEntry entry, string contents)
        {
            using (StreamWriter sw = new StreamWriter(entry.Open()))
            {
                sw.WriteLine(contents);
            }
        }
        private DateTime AddCheckEntry(ZipArchive zipArchive)
        {
            ZipArchiveEntry entry = zipArchive.CreateEntry("SILTranscriber", CompressionLevel.Fastest);
            DateTime dt = DateTime.UtcNow;
            WriteEntry(entry, dt.ToString("o"));
            return dt;
        }
        private void AddJsonEntry(ZipArchive zipArchive, string table, IList list, char sort)
        {
            ZipArchiveEntry entry = zipArchive.CreateEntry("data/" + sort + "_" + table + ".json", CompressionLevel.Fastest);
            WriteEntry(entry, jsonApiSerializer.Serialize(list));
        }
        private void AddEafEntry(ZipArchive zipArchive, string name, string eafxml)
        {
            if (!string.IsNullOrEmpty(eafxml))
            {
                ZipArchiveEntry entry = zipArchive.CreateEntry("media/" + Path.GetFileNameWithoutExtension(name) + ".eaf", CompressionLevel.Optimal);
                WriteEntry(entry, eafxml);
            }
        }
        private bool AddStreamEntry(ZipArchive zipArchive, Stream fileStream, string dir, string newName)
        {
            if (fileStream != null)
            {
                ZipArchiveEntry entry = zipArchive.CreateEntry(dir + newName, CompressionLevel.Optimal);
                using (Stream zipEntryStream = entry.Open())
                {
                    //Copy the attachment stream to the zip entry stream
                    fileStream.CopyTo(zipEntryStream);
                }
                return true;
            }
            return false;
        }
        private bool AddStreamEntry(ZipArchive zipArchive, string url, string dir, string newName)
        {
            return AddStreamEntry(zipArchive, GetStreamFromUrl(url), dir, newName);
        }
        private static Stream GetStreamFromUrl(string url)
        {
            byte[] imageData = null;
            try
            {
                using (WebClient wc = new System.Net.WebClient())
                    imageData = wc.DownloadData(url);
            }
            catch
            {
                return null;
            }
            return new MemoryStream(imageData);
        }
        private void AddOrgLogos(ZipArchive zipArchive, List<Organization> orgs)
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
        private void AddUserAvatars(ZipArchive zipArchive, List<User> users)
        {
            users.ForEach(u =>
            {
                if (!string.IsNullOrEmpty(u.avatarurl))
                {
                    AddStreamEntry(zipArchive, u.avatarurl, "avatars/", u.Id.ToString() + u.FamilyName + ".png");
                    //u.avatarurl = "avatars/" + u.Id.ToString() + u.FamilyName + ".png";
                }
            });
        }
        private bool AddMediaEaf(int check, DateTime dtBail, ref int completed, ZipArchive zipArchive, List<Mediafile> media)
        {
            if (DateTime.Now > dtBail) return false;
            if (completed <= check)
            {
                foreach (Mediafile m in media)
                   AddEafEntry(zipArchive, m.S3File, mediaService.EAF(m));
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
        private void AddFont(ZipArchive zipArchive, WebClient client, string cssfile)
        {
            string bucket = "https://s3.amazonaws.com/fonts.siltranscriber.org/";
            try
            {
                /* read the css file */
                string url = bucket + cssfile;
                string css = client.DownloadString(url);
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
                    url = css.Substring(start, end - start);
                    string fontfile = url.Substring(url.LastIndexOf("/") + 1);
                    url = bucket + fontfile;
                    AddStreamEntry(zipArchive, url, "fonts/", fontfile);
                    css = css.Substring(0, start + 1) + fontfile + css.Substring(end);
                }
                ZipArchiveEntry entry = zipArchive.CreateEntry("fonts/" + cssfile, CompressionLevel.Fastest);
                WriteEntry(entry, css);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Font file not found {0}", cssfile);
                Console.WriteLine(ex);
            }
        }
        private void AddFonts(ZipArchive zipArchive, IEnumerable<string> fonts)
        {
            using (WebClient client = new WebClient())
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
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(Path.GetInvalidFileNameChars()) + "'");
            string invalidReStr = string.Format(@"[{0}, ]+", invalidChars);

            string[] reservedWords = new[]
            {
        "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4",
        "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4",
        "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

            string sanitisedName = System.Text.RegularExpressions.Regex.Replace(filename, invalidReStr, "_");
            while (sanitisedName.IndexOf("__") > -1)
                sanitisedName = sanitisedName.Replace("__", "_");

            foreach (string reservedWord in reservedWords)
            {
                string reservedWordPattern = string.Format("^{0}(\\.|$)", reservedWord);
                sanitisedName = System.Text.RegularExpressions.Regex.Replace(sanitisedName, reservedWordPattern, "_reservedWord_$1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return sanitisedName;
        }
        private bool CheckAdd(int check, DateTime dtBail, ref int completed, ZipArchive zipArchive, string table, IList list, char sort)
        {
            Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail) return false;
            if (completed <= check)
            {
                AddJsonEntry(zipArchive, table, list, sort);
                completed++;
            }
            return true;
        }
        private FileResponse CheckProgress(int projectid, string fileName)
        {
            int startNext =0;
            string err = "";
            try
            {
                Stream ms = OpenFile(fileName + ".sss");
                StreamReader reader = new StreamReader(ms);
                string data = reader.ReadToEnd();
                if (data.IndexOf("|") > 0)
                {
                    err = data.Substring(data.IndexOf("|") + 1);
                    data = data.Substring(0, data.IndexOf("|"));
                }
                int.TryParse(data, out startNext);
            }
            catch
            {
                //it's not there yet...
                Logger.LogInformation("status file not available");
                startNext = 9;
            }
            if (startNext < 0)
            {
                try
                {
                    S3Response resp = _S3service.RemoveFile(fileName + ".sss", ExportFolder).Result;
                    resp = _S3service.RemoveFile(fileName + ".tmp", ExportFolder).Result;
                }
                catch { };
            }
            else
                startNext = Math.Max(startNext, 9);

            return new FileResponse()
            {
                Message = startNext == -2 ? err : fileName +".ptf",
                //get a signedurl for it if we're done
                FileURL = startNext == -1 ? _S3service.SignedUrlForGet(fileName + ".ptf", ExportFolder, ContentType).Message : "",
                Status = startNext == -1 ? System.Net.HttpStatusCode.OK : startNext == -2 ? System.Net.HttpStatusCode.RequestEntityTooLarge : System.Net.HttpStatusCode.PartialContent,
                ContentType = ContentType,
                Id = startNext,
            };
        }
        private Stream OpenFile(string fileName)
        {
            S3Response s3response  = _S3service.ReadObjectDataAsync(fileName, ExportFolder).Result;
            if (s3response.FileStream == null)
                throw (new Exception("Export in progress " + fileName + "not found."));
            return s3response.FileStream;
        }

        public FileResponse ExportProject(int projectid, int start)
        {
            int startNext = start;
            Logger.LogInformation($"{DateTime.Now}");
            //give myself 15 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(15);

            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == projectid);
            Project project = projects.First();
            string fileName = string.Format("Transcriber{0}_{1}_{2}" , CoerceValidFileName(project.Name), project.Id.ToString(), CurrentUser().Id) ;

            S3Response s3response;
            Stream ms;
            if (start > 8)
                return CheckProgress(projectid, fileName);

            if (start == 0)
            {
                ms = new MemoryStream();
                try
                {
                    S3Response resp = _S3service.RemoveFile(fileName + ".sss", ExportFolder).Result;
                }
                catch { };
            }
            else
            {
                ms = OpenFile(fileName + ".ptf");
            }
            using (ZipArchive zipArchive = new ZipArchive(ms,  ZipArchiveMode.Update, true))
            {
                if (start == 0)
                {
                    Dictionary<string, string> fonts = new Dictionary<string, string>();
                    fonts.Add("Charis SIL", "");
                    DateTime exported = AddCheckEntry(zipArchive);
                    AddJsonEntry(zipArchive, "activitystates", dbContext.Activitystates.ToList(), 'B');
                    AddJsonEntry(zipArchive, "integrations", dbContext.Integrations.ToList(), 'B');
                    AddJsonEntry(zipArchive, "plantypes", dbContext.Plantypes.ToList(), 'B');
                    AddJsonEntry(zipArchive, "projecttypes", dbContext.Projecttypes.ToList(), 'B');
                    AddJsonEntry(zipArchive, "roles", dbContext.Roles.ToList(), 'B');

                    //org
                    IQueryable<Organization> orgs = dbContext.Organizations.Where(o => o.Id == project.OrganizationId);
                    List<Organization> orgList = orgs.ToList();

                    AddOrgLogos(zipArchive, orgList);
                    AddJsonEntry(zipArchive, "organizations", orgList, 'B');

                    //groups
                    IQueryable<Group> groups = dbContext.Groups.Join(projects, g => g.Id, p => p.GroupId, (g, p) => g);
                    List<GroupMembership> gms = groups.Join(dbContext.Groupmemberships, g => g.Id, gm => gm.GroupId, (g, gm) => gm).Where(gm => !gm.Archived).ToList();
                    IEnumerable<User> users = gms.Join(dbContext.Users, gm => gm.UserId, u => u.Id, (gm, u) => u).Where(x => !x.Archived);

                    foreach (string font in gms.Where(gm => gm.Font != null).Select(gm => gm.Font))
                    {
                        fonts[font] = ""; //add it if it's not there
                    }
                    foreach (string font in projects.Where(p => p.DefaultFont != null).Select(p => p.DefaultFont))
                    {
                        fonts[font] = ""; //add it if it's not there
                    }
                    AddFonts(zipArchive, fonts.Keys);
                    //users
                    List<User> userList = users.ToList();
                    AddUserAvatars(zipArchive, userList);

                    AddJsonEntry(zipArchive, "groups", groups.Where(g => !g.Archived).ToList(), 'C');
                    //groupmemberships
                    AddJsonEntry(zipArchive, "groupmemberships", gms, 'D');
                    AddJsonEntry(zipArchive, "users", userList, 'A');

                    //organizationmemberships
                    IEnumerable<OrganizationMembership> orgmems = users.Join(dbContext.Organizationmemberships, u => u.Id, om => om.UserId, (u, om) => om).Where(om => om.OrganizationId == project.OrganizationId && !om.Archived);
                    AddJsonEntry(zipArchive, "organizationmemberships", orgmems.ToList(), 'C');

                    //projects
                    AddJsonEntry(zipArchive, "projects", projects.ToList(), 'D');
                    startNext=1;
                }
                do //give me something to break out of
                {
                    if (!CheckAdd(1, dtBail, ref startNext,  zipArchive, "projectintegrations", projects.Join(dbContext.Projectintegrations, p => p.Id, pi => pi.ProjectId, (p, pi) => pi).Where(x => !x.Archived).ToList(), 'E')) break;
                    //plans
                    IQueryable<Plan> plans = projects.Join(dbContext.Plans, p => p.Id, pl => pl.ProjectId, (p, pl) => pl).Where(x => !x.Archived);
                    if (!CheckAdd(2, dtBail, ref startNext, zipArchive, "plans", plans.ToList(), 'E')) break;
                    //sections
                    IQueryable<Section> sections = plans.Join(dbContext.Sections, p => p.Id, s => s.PlanId, (p, s) => s).Where(x => !x.Archived);
                    if (!CheckAdd(3, dtBail, ref startNext, zipArchive, "sections", sections.ToList(), 'F')) break;
                    //passages
                    IQueryable<Passage> passages = sections.Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p).Where(x => !x.Archived);
                    if (!CheckAdd(4, dtBail,  ref startNext, zipArchive, "passages", passages.ToList(), 'G')) break;
                    //passagestatechange
                    IQueryable<PassageStateChange> passagestatechanges = passages.Join(dbContext.Passagestatechanges, p => p.Id, psc => psc.PassageId, (p, psc) => psc);
                    if (!CheckAdd(5, dtBail, ref startNext, zipArchive, "passagestatechanges", passagestatechanges.ToList(), 'H')) break;
                    //mediafiles
                    IQueryable<Mediafile> mediafiles = plans.Join(dbContext.Mediafiles, p => p.Id, m => m.PlanId, (p, m) => m).Where(x => !x.Archived);
                    IQueryable<Mediafile> attachedmediafiles = passages.Join(dbContext.Mediafiles, p => p.Id, m => m.PassageId, (p, m) => m).Where(x => !x.Archived);
                    if (!CheckAdd(6, dtBail, ref startNext, zipArchive, "mediafiles", mediafiles.OrderBy(m => m.Id).ToList(), 'H')) break;

                    //pick just the highest version media per passage
                    attachedmediafiles = from m in attachedmediafiles group m by m.PassageId into grp select grp.OrderByDescending(m => m.VersionNumber).FirstOrDefault();
                    List<Mediafile> mediaList = attachedmediafiles.OrderBy(m => m.Id).ToList();
                    if (!AddMediaEaf(7, dtBail, ref startNext, zipArchive, mediaList)) break;
                    mediaList.ForEach(m =>
                    {
                        //S3File has just the filename
                        //AudioUrl has the signed GetUrl which has the path + filename as url (so spaces changed etc) + signed stuff
                        //change the audioUrl to have the offline path + filename
                        //change the s3File to have the onlinepath + filename
                        string tmp = m.AudioUrl;
                        m.AudioUrl = "media/" + m.S3File;
                        m.S3File = mediaService.DirectoryName(m) + "/" + m.S3File;
                    });
                    if (!CheckAdd(8, dtBail, ref startNext, zipArchive, "attachedmediafiles", mediaList, 'Z')) break;
                } while (false);
            }
            ms.Position = 0;
            fileName += (startNext == 9 ? ".tmp" : ".ptf"); //tmp signals the trigger to add mediafiles
            s3response = _S3service.UploadFileAsync(ms, true, ContentType, fileName, ExportFolder).Result;
            if (s3response.Status == HttpStatusCode.OK)
            {
                Logger.LogInformation($"{DateTime.Now}, {startNext}");
                return new FileResponse()
                {
                    Message = fileName,
                    FileURL = "",
                    Status = HttpStatusCode.PartialContent,
                    ContentType = ContentType,
                    Id = startNext,
                };
            }
            else
            {
                throw new Exception(s3response.Message);
            }
        }
        public FileResponse ImportFileURL(string sFile)
        {
            string extension = Path.GetExtension(sFile);
            string ContentType = "application/" + extension;
            // Project project = dbContext.Projects.Where(p => p.Id == id).First();
            string fileName = string.Format("{0}_{1}.{2}", Path.GetFileNameWithoutExtension(sFile), DateTime.Now.Ticks, extension);
            //get a signedurl for it now
            return new FileResponse()
            {
                Message = fileName,
                FileURL = _S3service.SignedUrlForPut(fileName, ImportFolder, ContentType).Message,
                Status = System.Net.HttpStatusCode.OK,
                ContentType = ContentType,
            };
        }
        private string ProjectDeletedReport(Project project)
        {
            return ChangesReport("project", "\"deleted\"", jsonApiSerializer.Serialize(project));
        }
        private string ChangesReport(string type, string online, string imported)
        {
            return "{\"type\":\"" + type + "\", \"online\": " + online + ", \"imported\": " + imported + "}";
        }
        private string UserChangesReport(User online, User imported)
        {
            return ChangesReport("user",  jsonApiSerializer.Serialize(online),  jsonApiSerializer.Serialize(imported));
        }
        private string SectionChangesReport(Section online, Section imported)
        {
            Dictionary<string, string> changes = new Dictionary<string, string>();

            if ((online.EditorId != imported.EditorId && online.EditorId != null) || 
                (online.TranscriberId != imported.TranscriberId && online.TranscriberId != null) || 
                online.State != imported.State )
            {
                return ChangesReport( "section",  jsonApiSerializer.Serialize(online), jsonApiSerializer.Serialize(imported));
            }
            return "";
       }
        private string PassageChangesReport(Passage online, Passage imported)
        {
            if (online.State != imported.State)
            {
                return ChangesReport("passage",  jsonApiSerializer.Serialize(online), jsonApiSerializer.Serialize(imported));
            }
            return "";
        }
        private string MediafileChangesReport(Mediafile online, Mediafile imported)
        {
            Dictionary<string, string> changes = new Dictionary<string, string>();
            if (online.Transcription != imported.Transcription && online.Transcription != null)
            {
                Mediafile copy = (Mediafile) online.ShallowCopy();
                copy.AudioUrl = "";
                return ChangesReport("mediafile", jsonApiSerializer.Serialize(copy), jsonApiSerializer.Serialize(imported));
            }            
            return "";
        }
        private string GrpMemChangesReport(GroupMembership online, GroupMembership imported)
        {
            if (online.FontSize != imported.FontSize)
            {
                return ChangesReport("groupmembership", jsonApiSerializer.Serialize(online), jsonApiSerializer.Serialize(imported));
            }
            return "";
        }
        public async Task<FileResponse> ImportFileAsync(string sFile)
        {
            S3Response response = await _S3service.ReadObjectDataAsync(sFile, "imports");
            ZipArchive archive = new ZipArchive(response.FileStream);
            List<string> report = new List<string>();
            List<string> errors = new List<string>();
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                ZipArchive zipentry = new ZipArchive(entry.Open());
                FileResponse fr = await ProcessImportFileAsync(zipentry, 0, entry.Name);
                if (fr.Status == HttpStatusCode.OK)
                {   //remove beginning and ending brackets
                    string msg = fr.Message.StartsWith("[") ?  fr.Message.Substring(1, fr.Message.Length - 2) : fr.Message;
                    report.Add(msg);
                }
                else
                {
                    errors.Add(JsonConvert.SerializeObject(fr));
                }
            }
            report.RemoveAll(s => s.Length == 0);
            errors.RemoveAll(s => s.Length == 0);
            if (errors.Count() > 0)
                return errorResponse("{\"errors\": [" + string.Join(",", errors) + "], \"report\": [" + string.Join(", ", report) + "]}", sFile);

            return new FileResponse()
            {
                Message = "[" + string.Join(",", report) + "]",
                FileURL = sFile,
                Status = HttpStatusCode.OK,
                ContentType = "application/itf",
            };
        }
        public async Task<FileResponse> ImportFileAsync(int projectid, string sFile)
        {
            S3Response response = await _S3service.ReadObjectDataAsync(sFile, "imports");
            ZipArchive archive = new ZipArchive(response.FileStream);
            return await ProcessImportFileAsync(archive, projectid, sFile);
        }
        private FileResponse projectDeletedResponse(string msg, string sFile)
        {
            return errorResponse(msg, sFile, System.Net.HttpStatusCode.MovedPermanently);
        }
        private FileResponse NotCurrentProjectResponse(string msg, string sFile)
        {
            return errorResponse(msg, sFile, System.Net.HttpStatusCode.NotAcceptable);
        }
        private FileResponse errorResponse(string msg, string sFile, HttpStatusCode status = System.Net.HttpStatusCode.UnprocessableEntity)
        {
            const string ContentType = "application/itf";
            return new FileResponse()
            {
                Message = msg,
                FileURL = sFile,
                Status = status,
                ContentType = ContentType,
            };
        }
        private async Task<FileResponse> ProcessImportFileAsync(ZipArchive archive, int projectid, string sFile)
        {
            DateTime sourceDate;
            List<string> report = new List<string>();
            List<string> deleted = new List<string>();
            try
            {
                ZipArchiveEntry checkEntry = archive.GetEntry("SILTranscriberOffline");
                //var exportTime = new StreamReader(checkEntry.Open()).ReadToEnd();
            }
            catch
            {
                return errorResponse("SILTranscriberOffline not present", sFile);
            }
            try
            {
                ZipArchiveEntry sourceEntry = archive.GetEntry("SILTranscriber");
                sourceDate = Convert.ToDateTime(new StreamReader(sourceEntry.Open()).ReadToEnd());
            }
            catch
            {
                return errorResponse("SILTranscriber not present", sFile);
            }
            //check project if provided
            Project project;
            try
            {
                ZipArchiveEntry projectsEntry = archive.GetEntry("data/D_projects.json");
                if (projectsEntry == null)
                    return errorResponse("Project data not present", sFile);
                string json = new StreamReader(projectsEntry.Open()).ReadToEnd();
                List<Project> projects = jsonApiDeSerializer.DeserializeList<Project>(json);
                project = dbContext.Projects.Find(projects[0].Id);
                if (projectid > 0)
                {
                    if (projectid != project.Id)
                    {
                        return NotCurrentProjectResponse(project.Name, sFile);
                    }
                    if (project.Archived)
                        return projectDeletedResponse(project.Name, sFile);
                } 
                if (project==null)
                    return projectDeletedResponse(projects[0].Name, sFile);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return errorResponse("Invalid ITF File - error finding project -" + ex.Message, sFile);
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
                        string data = new StreamReader(entry.Open()).ReadToEnd();
                        string name = Path.GetFileNameWithoutExtension(entry.Name.Substring(2));
                        switch (name)
                        {
                            case "users":
                                List<User> users = jsonApiDeSerializer.DeserializeList<User>(data);
                                foreach (User u in users)
                                {
                                    User user = dbContext.Users.Find(u.Id);
                                    if (!user.Archived && user.DateUpdated != u.DateUpdated)
                                    {
                                        if (user.DateUpdated > sourceDate && user.DateUpdated != u.DateUpdated)
                                            report.Add(UserChangesReport(user, u));

                                        user.DigestPreference = u.DigestPreference;
                                        user.FamilyName = u.FamilyName;
                                        user.GivenName = u.GivenName;
                                        user.Locale = u.Locale;
                                        user.Name = u.Name;
                                        user.NewsPreference = u.NewsPreference;
                                        user.Phone = u.Phone;
                                        user.playbackspeed = u.playbackspeed;
                                        user.progressbartypeid = u.progressbartypeid;
                                        user.timercountup = u.timercountup;
                                        user.Timezone = u.Timezone;
                                        user.uilanguagebcp47 = u.uilanguagebcp47;
                                        user.LastModifiedBy = u.LastModifiedBy;
                                        user.DateUpdated = DateTime.UtcNow;
                                        /* TODO: figure out if the avatar needs uploading */
                                        dbContext.Users.Update(user);
                                    }
                                };
                                break;

                            case "sections":
                                List<Section> sections = jsonApiDeSerializer.DeserializeList<Section>(data);
                                foreach (Section s in sections)
                                {
                                    Section section = dbContext.Sections.Find(s.Id);
                                    if (!section.Archived)
                                    {
                                        if (section.DateUpdated > sourceDate)
                                            report.Add(SectionChangesReport(section, s));

                                        section.EditorId = s.EditorId;
                                        section.TranscriberId = s.TranscriberId;
                                        section.State = s.State;
                                        section.LastModifiedBy = s.LastModifiedBy;
                                        section.DateUpdated = DateTime.UtcNow;
                                        dbContext.Sections.Update(section);
                                    }
                                };
                                break;

                            case "passages":
                                List<Passage> passages = jsonApiDeSerializer.DeserializeList<Passage>(data);
                                int currentuser = CurrentUser().Id;
                                foreach (Passage p in passages)
                                {
                                    Passage passage = dbContext.Passages.Find(p.Id);
                                    if (!passage.Archived && passage.State != p.State)
                                    {
                                        if (passage.DateUpdated > sourceDate)
                                        {
                                            report.Add(PassageChangesReport(passage, p));
                                        }
                                        passage.State = p.State;
                                        passage.LastModifiedBy = p.LastModifiedBy;
                                        passage.DateUpdated = DateTime.UtcNow;
                                        dbContext.Passages.Update(passage);
                                        PassageStateChange psc = new PassageStateChange();
                                        psc.Comments = "Imported";  //TODO Localize
                                        psc.DateCreated = passage.DateUpdated;
                                        psc.DateUpdated = passage.DateUpdated;
                                        psc.LastModifiedBy = currentuser;
                                        psc.PassageId = passage.Id;
                                        psc.State = passage.State;
                                        dbContext.Passagestatechanges.Add(psc);
                                    }
                                };
                                break;

                            case "mediafiles":
                                List<Mediafile> mediafiles = jsonApiDeSerializer.DeserializeList<Mediafile>(data);
                                /*
                                var newFiles = mediafiles.Where(e => e.StringId == "");
                                if (newFiles.Count() > 0)
                                {
                                    dbContext.Mediafiles.AddRange(newFiles);
                                    // upload the file 
                                } */
                                foreach (Mediafile m in mediafiles)
                                {
                                    Mediafile mediafile;
                                    if (m.Id > 0)
                                    {
                                        mediafile = dbContext.Mediafiles.Find(m.Id);
                                        if (!mediafile.Archived && mediafile.Transcription != m.Transcription)
                                        {
                                            if (mediafile.DateUpdated > sourceDate)
                                                report.Add(MediafileChangesReport(mediafile, m));
                                            mediafile.Position = m.Position;
                                            mediafile.Transcription = m.Transcription;
                                            mediafile.LastModifiedBy = m.LastModifiedBy;
                                            mediafile.DateUpdated = DateTime.UtcNow;
                                            dbContext.Mediafiles.Update(mediafile);
                                        }
                                    }
                                    else
                                    {
                                        /* the only way this happens now is on a reopen.  If we start allowing them to actually replace the mediafile,
                                         * we'll have to upload it from the zip file and create a new s3 file */
                                        mediafile = dbContext.Mediafiles.Where(p => p.PassageId == m.PassageId && !p.Archived).OrderByDescending(p => p.VersionNumber).FirstOrDefault();
                                        Mediafile newmf = (Mediafile)mediafile.ShallowCopy();
                                        newmf.Transcription = m.Transcription;
                                        newmf.Id = 0;
                                        newmf.LastModifiedBy = m.LastModifiedBy;
                                        newmf.DateCreated = m.DateCreated;
                                        newmf.VersionNumber = m.VersionNumber;
                                        newmf.DateUpdated = DateTime.UtcNow;
                                        dbContext.Mediafiles.Add(newmf);
                                    }
                                };
                                break;

                            case "groupmemberships":
                                List<GroupMembership> grpmems = jsonApiDeSerializer.DeserializeList<GroupMembership>(data);
                                foreach (GroupMembership gm in grpmems)
                                {
                                    GroupMembership grpmem = dbContext.Groupmemberships.Find(gm.Id);
                                    if (!grpmem.Archived && grpmem.FontSize != gm.FontSize)
                                    {
                                        if (grpmem.DateUpdated > sourceDate)
                                            report.Add(GrpMemChangesReport(grpmem, gm));
                                        grpmem.FontSize = gm.FontSize;
                                        grpmem.LastModifiedBy = gm.LastModifiedBy;
                                        grpmem.DateUpdated = DateTime.UtcNow;
                                        dbContext.Groupmemberships.Update(grpmem);
                                    }
                                };
                                break;

                            /*  Local changes to project integrations should just stay local
                            case "projectintegrations":
                                List<ProjectIntegration> pis = jsonApiDeSerializer.DeserializeList<ProjectIntegration>(data);
                                break;
                            */

                            case "passagestatechanges":
                                List<PassageStateChange> pscs = jsonApiDeSerializer.DeserializeList<PassageStateChange>(data);
                                foreach (PassageStateChange psc in pscs)
                                {
                                    //see if it's already there...
                                    IQueryable<PassageStateChange> dups = dbContext.Passagestatechanges.Where(c => c.PassageId == psc.PassageId && c.DateCreated == psc.DateCreated && c.State == psc.State);
                                    if (dups.Count() == 0)
                                    {   /* if I send psc in directly, the id goes wonky...must be *something* different in the way it is initialized (tried setting id=0), so copy relevant info here */
                                        dbContext.Passagestatechanges.Add(new PassageStateChange
                                        {
                                            PassageId = psc.PassageId,
                                            State = psc.State,
                                            DateCreated = psc.DateCreated,
                                            Comments = psc.Comments,
                                            LastModifiedBy = psc.LastModifiedBy,
                                            DateUpdated = DateTime.UtcNow,
                                        });
                                    };

                                };
                                break;
                        }
                    }
                }
                int ret = await dbContext.SaveChangesNoTimestampAsync();
                report.RemoveAll(s => s.Length == 0);

                return new FileResponse()
                {
                    Message = "[" + string.Join(",", report) + "]",
                    FileURL = sFile,
                    Status =HttpStatusCode.OK,
                    ContentType = ContentType,
                };
            }
            catch (Exception ex)
            {
                return errorResponse(ex.Message + (ex.InnerException != null && ex.InnerException.Message != "" ? "=>" + ex.InnerException.Message : ""), sFile);
            }
        }
    }
}
