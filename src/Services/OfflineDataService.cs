using JsonApiDotNetCore.Data;
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

namespace SIL.Transcriber.Services
{
    public class OfflineDataService: IOfflineDataService
    {
        protected readonly AppDbContext dbContext;
        protected readonly IJsonApiSerializer jsonApiSerializer;
        protected readonly IJsonApiDeSerializer jsonApiDeSerializer;
        protected readonly MediafileService mediaService;
        private IS3Service _S3service;
        const string ImportFolder = "imports";
        const string ExportFolder = "exports";
        protected ILogger<OfflineDataService> Logger { get; set; }

        public OfflineDataService(IDbContextResolver contextResolver, IJsonApiSerializer jsonSer, IJsonApiDeSerializer jsonDeser, MediafileService MediaService, IS3Service service, ILoggerFactory loggerFactory)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            jsonApiSerializer = jsonSer;
            jsonApiDeSerializer = jsonDeser;
            mediaService = MediaService;
            _S3service = service;
            this.Logger = loggerFactory.CreateLogger<OfflineDataService>();
        }
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
        private void AddStreamEntry(ZipArchive zipArchive, Stream fileStream, string dir, string newName)
        {
            ZipArchiveEntry entry = zipArchive.CreateEntry(dir + newName, CompressionLevel.Optimal);
            using (Stream zipEntryStream = entry.Open())
            {
                //Copy the attachment stream to the zip entry stream
                fileStream.CopyTo(zipEntryStream);
            }
        }
        private void AddStreamEntry(ZipArchive zipArchive, string url, string dir, string newName)
        {
            AddStreamEntry(zipArchive, GetStreamFromUrl(url), dir, newName);
        }
        private static Stream GetStreamFromUrl(string url)
        {
            byte[] imageData = null;

            using (WebClient wc = new System.Net.WebClient())
                imageData = wc.DownloadData(url);

            return new MemoryStream(imageData);
        }
        private void AddOrgLogos(ZipArchive zipArchive, List<Organization> orgs)
        {
            orgs.ForEach(o =>
            {
                if (!string.IsNullOrEmpty(o.LogoUrl))
                {
                    AddStreamEntry(zipArchive, o.LogoUrl, "logos/", o.Slug + ".png");
                    o.LogoUrl = "logos/" + o.Slug + ".png";
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
                    u.avatarurl = "avatars/" + u.Id.ToString() + u.FamilyName + ".png";
                }
            });
        }
   
        private void AddMedia(ZipArchive zipArchive, List<Mediafile> media)
        {
            media.ForEach( m =>
            {
                if (!string.IsNullOrEmpty(m.S3File))
                {
                    S3Response response = mediaService.GetFile(m.Id).Result;
                    AddStreamEntry(zipArchive, response.FileStream, "media/", m.S3File);
                    m.AudioUrl = "media/" + m.S3File;
                    AddEafEntry(zipArchive, m.S3File, mediaService.EAF(m));
                }
            });
        }
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
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidReStr = string.Format(@"[{0}]+", invalidChars);

            string[] reservedWords = new[]
            {
        "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4",
        "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4",
        "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

            string sanitisedNamePart = System.Text.RegularExpressions.Regex.Replace(filename, invalidReStr, "_");
            foreach (string reservedWord in reservedWords)
            {
                string reservedWordPattern = string.Format("^{0}(\\.|$)", reservedWord);
                sanitisedNamePart = System.Text.RegularExpressions.Regex.Replace(sanitisedNamePart, reservedWordPattern, "_reservedWord_$1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return sanitisedNamePart;
        }
        private FileResponse Export(int orgid, int projectid = 0)
        {
            //export this organization
            IQueryable<Organization> orgs = dbContext.Organizations.Where(o => o.Id == orgid);
            IQueryable<Project> projects;
            if (orgs.Count() == 0)
            {
                return new FileResponse()
                {
                    Status = System.Net.HttpStatusCode.NotFound,
                    Message = "Organization does not exist. " + orgid.ToString()
                };
            }

            if (projectid != 0)
                projects = dbContext.Projects.Where(p => p.Id == projectid);
            else
                projects = dbContext.Projects.Where(p => p.OrganizationId == orgid && !p.Archived);

            MemoryStream ms = new MemoryStream();
            using (ZipArchive zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                Dictionary<string, string> fonts = new Dictionary<string, string>();
                fonts.Add("Charis SIL", "");

                DateTime exported = AddCheckEntry(zipArchive);

                //org
                List<Organization> orgList = orgs.ToList();
                AddOrgLogos(zipArchive, orgList);
                AddJsonEntry(zipArchive, "organizations", orgList, 'B');


                //groups
                IQueryable<Group> groups = dbContext.Groups.Join(projects, g => g.Id, p => p.GroupId, (g, p) => g);
                AddJsonEntry(zipArchive, "groups", groups.Where(g =>!g.Archived).ToList(), 'C');

                //groupmemberships
                List<GroupMembership> gms = groups.Join(dbContext.Groupmemberships, g => g.Id, gm => gm.GroupId, (g, gm) => gm).Where(gm => !gm.Archived).ToList();
                AddJsonEntry(zipArchive, "groupmemberships", gms, 'D');
                foreach( string font in gms.Where(gm => gm.Font != null).Select(gm => gm.Font))
                {
                    fonts[font] = ""; //add it if it's not there
                }

                //users
                IEnumerable<User> users = gms.Join(dbContext.Users, gm => gm.UserId, u => u.Id, (gm, u) => u).Where(x => !x.Archived);
                List<User> userList = users.ToList();
                AddUserAvatars(zipArchive, userList);
                AddJsonEntry(zipArchive, "users", userList, 'A');

                //organizationmemberships
                IEnumerable<OrganizationMembership> orgmems = users.Join(dbContext.Organizationmemberships, u => u.Id, om => om.UserId, (u, om) => om).Where(om => om.OrganizationId == orgid && !om.Archived);
                AddJsonEntry(zipArchive, "organizationmemberships", orgmems.ToList(), 'C');

                //projects

                projects.ToList().ForEach(p => {
                    p.DateExported = exported;
                    dbContext.Projects.Update(p);
                });
                AddJsonEntry(zipArchive, "projects", projects.ToList(), 'D');
                foreach (string font in projects.Where(p => p.DefaultFont != null).Select(p => p.DefaultFont))
                {
                    fonts[font] = ""; //add it if it's not there
                }
                AddFonts(zipArchive, fonts.Keys);

                //projectintegrations
                AddJsonEntry(zipArchive, "projectintegrations", projects.Join(dbContext.Projectintegrations, p => p.Id, pi => pi.ProjectId, (p, pi) => pi).Where(x => !x.Archived).ToList(), 'E');
                //plans
                IQueryable<Plan> plans = projects.Join(dbContext.Plans, p => p.Id, pl => pl.ProjectId, (p, pl) => pl).Where(x => !x.Archived);
                AddJsonEntry(zipArchive, "plans", plans.ToList(), 'E');
                //sections
                IQueryable<Section> sections = plans.Join(dbContext.Sections, p => p.Id, s => s.PlanId, (p, s) => s).Where(x => !x.Archived);
                AddJsonEntry(zipArchive, "sections", sections.ToList(), 'F');
                //passages
                IQueryable<Passage> passages = sections.Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p).Where(x => !x.Archived);

                AddJsonEntry(zipArchive, "passages", passages.ToList(), 'G');
                //mediafiles
                IQueryable<Mediafile> mediafiles = passages.Join(dbContext.Mediafiles, p => p.Id, m => m.PassageId, (p, m) => m).Where(x => !x.Archived);
                //pick just the highest version media per passage
                mediafiles = from m in mediafiles group m by m.PassageId into grp select grp.OrderByDescending(m => m.VersionNumber).FirstOrDefault();
                List < Mediafile > mediaList = mediafiles.ToList();
                AddMedia(zipArchive, mediaList);
                AddJsonEntry(zipArchive, "mediafiles", mediaList, 'H');
                //passagestatechange
                IQueryable<PassageStateChange> passagestatechanges = passages.Join(dbContext.Passagestatechanges, p => p.Id, psc => psc.PassageId, (p, psc) => psc);
                AddJsonEntry(zipArchive, "passagestatechanges", passagestatechanges.ToList(), 'H');

                //ALL
                //activitystates
                AddJsonEntry(zipArchive, "activitystates", dbContext.Activitystates.ToList(), 'B');
                //integrations
                AddJsonEntry(zipArchive, "integrations", dbContext.Integrations.ToList(), 'B');
                //projecttypes
                AddJsonEntry(zipArchive, "projecttypes", dbContext.Projecttypes.ToList(), 'B');
                //plantypes
                AddJsonEntry(zipArchive, "plantypes", dbContext.Plantypes.ToList(), 'B');
                //roles
                AddJsonEntry(zipArchive, "roles", dbContext.Roles.ToList(), 'B');
            }
            ms.Position = 0;
            const string ContentType = "application/ptf";
            string fileName = projectid != 0 ? string.Format("Transcriber_{0}.ptf", CoerceValidFileName(projects.First().Name)) : string.Format("TranscriberOrg_{0}.ptf", CoerceValidFileName(orgs.First().Name));

            S3Response s3response = _S3service.UploadFileAsync(ms, true, ContentType, fileName, ExportFolder).Result;
            if (s3response.Status == System.Net.HttpStatusCode.OK)
            {
                //get a signedurl for it now
                return new FileResponse()
                {
                    Message = fileName,
                    FileURL = _S3service.SignedUrlForGet(fileName, ExportFolder, ContentType).Message,
                    Status = System.Net.HttpStatusCode.OK,
                    ContentType = ContentType,
                };
            }
            else
            {
                return s3response;
            }
        }
        public FileResponse ExportOrganization(int orgid)
        {
            return Export(orgid);
        }
        public FileResponse ExportProject(int id)
        {
            Project project = dbContext.Projects.Where(p => p.Id == id).First();
            return Export(project.OrganizationId, id);          
        }

        public FileResponse ImportFileURL(string sFile)
        {
            const string ContentType = "application/itf";
            // Project project = dbContext.Projects.Where(p => p.Id == id).First();
            string fileName = string.Format("{0}_{1}.itf", Path.GetFileNameWithoutExtension(sFile), DateTime.Now.Ticks);
            //get a signedurl for it now
            return new FileResponse()
            {
                Message = fileName,
                FileURL = _S3service.SignedUrlForPut(fileName, ImportFolder, ContentType).Message,
                Status = System.Net.HttpStatusCode.OK,
                ContentType = ContentType,
            };
        }
        private string UserChangesReport(User online, User imported)
        {
            Dictionary<string, string> changes = new Dictionary<string, string>();
            if (online.Name != imported.Name)
            {
                changes.Add("Previous Name", online.Name);
                changes.Add("Imported Name", imported.Name);
            }
            if (online.FamilyName != imported.FamilyName)
            {
                changes.Add("Previous FamilyName", online.FamilyName);
                changes.Add("Imported FamilyName", imported.FamilyName);
            }
            if (online.GivenName != imported.GivenName)
            {
                changes.Add("Previous GivenName", online.GivenName);
                changes.Add("Imported GivenName", imported.GivenName);
            }
            if (online.Phone != imported.Phone)
            {
                changes.Add("Previous Phone", online.Phone);
                changes.Add("Imported Phone", imported.Phone);
            }
            if (online.playbackspeed != imported.playbackspeed)
            {
                changes.Add("Previous playbackspeed", online.playbackspeed.ToString());
                changes.Add("Imported playbackspeed", imported.playbackspeed.ToString());
            }
            if (online.progressbartypeid != imported.progressbartypeid)
            {
                changes.Add("Previous progressbartypeid", online.progressbartypeid.ToString());
                changes.Add("Imported progressbartypeid", imported.progressbartypeid.ToString());
            }
            if (online.timercountup != imported.timercountup)
            {
                changes.Add("Previous timercountup", online.timercountup.ToString());
                changes.Add("Imported timercountup", imported.timercountup.ToString());
            }
            if (online.Timezone != imported.Timezone)
            {
                changes.Add("Previous Timezone", online.Timezone.ToString());
                changes.Add("Imported Timezone", imported.Timezone.ToString());
            }
            if (online.Locale != imported.Locale)
            {
                changes.Add("Previous Locale", online.Locale.ToString());
                changes.Add("Imported Locale", imported.Locale.ToString());
            }
            if (online.uilanguagebcp47 != imported.uilanguagebcp47)
            {
                changes.Add("Previous uilanguagebcp47", online.uilanguagebcp47.ToString());
                changes.Add("Imported uilanguagebcp47", imported.uilanguagebcp47.ToString());
            }
            if (online.NewsPreference != imported.NewsPreference)
            {
                changes.Add("Previous NewsPreference", online.NewsPreference.ToString());
                changes.Add("Imported NewsPreference", imported.NewsPreference.ToString());
            }
            if (online.DigestPreference != imported.DigestPreference)
            {
                changes.Add("Previous DigestPreference", online.DigestPreference.ToString());
                changes.Add("Imported DigestPreference", imported.DigestPreference.ToString());
            }
            if (changes.Count > 0)
                return "User: " + online.Email + Environment.NewLine + JsonConvert.SerializeObject(changes, Formatting.Indented) + Environment.NewLine;
            return "";
        }
        private string SectionChangesReport(Section online, Section imported)
        {
            Dictionary<string, string> changes = new Dictionary<string, string>();

            if (online.EditorId != imported.EditorId)
            {
                changes.Add("Previous Editor", dbContext.Users.Find(online.EditorId).Name) ;
                changes.Add("Imported Editor", dbContext.Users.Find(imported.EditorId).Name);
            }
            if (online.TranscriberId != imported.TranscriberId)
            {
                changes.Add("Previous Transcriber", dbContext.Users.Find(online.EditorId).Name);
                changes.Add("Imported Transcriber", dbContext.Users.Find(imported.EditorId).Name);

            }
            if (online.State != imported.State)
            {
                changes.Add("Previous State", online.State);  //TODO fix the state display
                changes.Add("Imported State", imported.State);
            }
            if (changes.Count > 0)
                return "Section: " + dbContext.Plans.Find(online.PlanId).Name + ":" + online.Sequencenum + Environment.NewLine + JsonConvert.SerializeObject(changes, Formatting.Indented) + Environment.NewLine;
            return "";
       }
        private string PassageChangesReport(Passage online, Passage imported)
        {
            Dictionary<string, string> changes = new Dictionary<string, string>();
            if (online.State != imported.State)
            {
                changes.Add("Previous State", online.State);  //TODO fix the state display
                changes.Add("Imported State", imported.State);
            }
            if (changes.Count > 0)
                return "Passage: " + online.Sequencenum + " " + online.Book + " " + online.Reference + Environment.NewLine + JsonConvert.SerializeObject(changes, Formatting.Indented) + Environment.NewLine;
            return "";
        }
        private string MediafileChangesReport(Mediafile online, Mediafile imported)
        {
            Dictionary<string, string> changes = new Dictionary<string, string>();
            if (online.Transcription != imported.Transcription && online.Transcription != null)
            {
                changes.Add("Previous Transcription", online.Transcription);
                changes.Add("Imported Transcription", imported.Transcription);
            }
            if (changes.Count > 0)
                return "Transcription:" + Environment.NewLine + JsonConvert.SerializeObject(changes, Formatting.Indented) + Environment.NewLine;
            return "";
        }
        private string GrpMemChangesReport(GroupMembership online, GroupMembership imported)
        {
            Dictionary<string, string> changes = new Dictionary<string, string>();
            if (online.FontSize != imported.FontSize)
            {
                changes.Add("Previous FontSize", online.FontSize);
                changes.Add("Imported FontSize", imported.FontSize);
            }
            /* simplify the object to report on */
            if (changes.Count > 0)
                return "Preferences:" + dbContext.Groups.Find(online.GroupId).Name + "/" + dbContext.Users.Find(online.UserId).Name + Environment.NewLine + JsonConvert.SerializeObject(changes, Formatting.Indented) + Environment.NewLine;
            return "";
        }
        public async Task<FileResponse> ImportFileAsync(int projectid, string sFile)
        {
            const string ContentType = "application/itf";
            DateTime sourceDate;

            S3Response response = await _S3service.ReadObjectDataAsync(sFile, "imports");
            ZipArchive archive = new ZipArchive(response.FileStream);
            string report = "";
            try
            {
                ZipArchiveEntry checkEntry = archive.GetEntry("SILTranscriberOffline");
                //var exportTime = new StreamReader(checkEntry.Open()).ReadToEnd();
            }
            catch
            {
                return new FileResponse()
                {
                    Message = "Invalid ITF File - SILTranscriberOffline not present",
                    FileURL = sFile,
                    Status = System.Net.HttpStatusCode.UnprocessableEntity,
                    ContentType = ContentType,
                };
            }
            try
            {
                ZipArchiveEntry sourceEntry = archive.GetEntry("SILTranscriber");
                sourceDate = Convert.ToDateTime(new StreamReader(sourceEntry.Open()).ReadToEnd());
            }
            catch
            {
                return new FileResponse()
                {
                    Message = "Invalid ITF File - SILTranscriber not present",
                    FileURL = sFile,
                    Status = System.Net.HttpStatusCode.UnprocessableEntity,
                    ContentType = ContentType,
                };
            }
            //check project
            Project project;
            try
            {
                ZipArchiveEntry projectsEntry = archive.GetEntry("data/D_projects.json");
                if (projectsEntry==null)
                    return new FileResponse()
                    {
                        Message = "Invalid ITF File - projects data not present",
                        FileURL = sFile,
                        Status = System.Net.HttpStatusCode.UnprocessableEntity,
                        ContentType = ContentType,
                    };

                List<Project> projects = jsonApiDeSerializer.DeserializeList<Project>(new StreamReader(projectsEntry.Open()).ReadToEnd());
                project = projects.Find(p => p.Id == projectid);
                if (project==null)
                    return new FileResponse()
                    {
                        Message = "This ITF File does not contain the current Project",
                        FileURL = sFile,
                        Status = (HttpStatusCode)450,
                        ContentType = ContentType,
                    };
                
            }
            catch
            {
                return new FileResponse()
                {
                    Message = "Invalid ITF File - projects file not present",
                    FileURL = sFile,
                    Status = System.Net.HttpStatusCode.UnprocessableEntity,
                    ContentType = ContentType,
                };
            }

            try
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
                            IEnumerable<User> myUsers = users.Join(dbContext.Groupmemberships.Where(gm => gm.GroupId == project.GroupId), u => u.Id, grpmem => grpmem.UserId, (u, grpmem) => u);
                            foreach(User u in myUsers)
                            {
                                User user = dbContext.Users.Find(u.Id);
                                
                                if (user.DateUpdated > sourceDate)
                                    report += UserChangesReport(user, u);

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
                                user.LastModifiedOrigin = "electron";
                                user.DateUpdated = DateTime.UtcNow; 
                                /* TODO: figure out if the avatar needs uploading */
                                dbContext.Users.Update(user);
                            };
                            break;

                        case "sections":
                            List<Section> sections = jsonApiDeSerializer.DeserializeList<Section>(data);
                            IEnumerable<Section> mysections = sections.Join(dbContext.Plans.Where(p => p.ProjectId == projectid), s => s.PlanId, pl => pl.Id, (s, pl) => s);
                            foreach(Section s in mysections)
                            {
                                Section section = dbContext.Sections.Find(s.Id);
                                if (section.DateUpdated > sourceDate)
                                        report += SectionChangesReport(section, s);

                                section.EditorId = s.EditorId;
                                section.TranscriberId = s.TranscriberId;
                                section.State = s.State;
                                section.LastModifiedBy = s.LastModifiedBy;
                                section.LastModifiedOrigin = "electron";
                                section.DateUpdated = DateTime.UtcNow; 
                                dbContext.Sections.Update(section);
                            };
                            break;

                        case "passages":
                            List<Passage> passages = jsonApiDeSerializer.DeserializeList<Passage>(data);
                            IQueryable<Passage> mypassages = dbContext.Plans.Where(pl => pl.ProjectId == projectid).Join(dbContext.Sections, p => p.Id, s => s.PlanId, (p, s) => s).Join(passages, s => s.Id, p => p.SectionId, (s, p) => p);
                            foreach (Passage p in mypassages)
                            {
                                Passage passage = dbContext.Passages.Find(p.Id);
                                if (passage.State != p.State)
                                {
                                    if (passage.DateUpdated > sourceDate)
                                        report += PassageChangesReport(passage, p);
                                    passage.State = p.State;
                                    passage.LastModifiedBy = p.LastModifiedBy;
                                    passage.LastModifiedOrigin = "electron";
                                    passage.DateUpdated = DateTime.UtcNow;
                                    dbContext.Passages.Update(passage);
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
                            IEnumerable<Mediafile> mymediafiles = mediafiles.Join(dbContext.Plans.Where(pl => pl.ProjectId == projectid), mf => mf.PlanId, p => p.Id, (mf, p) => mf);
                            foreach(Mediafile m in mymediafiles)
                            {
                                Mediafile mediafile = dbContext.Mediafiles.Find(m.Id);
                                if (mediafile.Transcription != m.Transcription || mediafile.Position != m.Position)
                                {
                                    if (mediafile.DateUpdated > sourceDate)
                                        report += MediafileChangesReport(mediafile, m);
                                    mediafile.Position = m.Position;
                                    mediafile.Transcription = m.Transcription;
                                    mediafile.LastModifiedBy = m.LastModifiedBy;
                                    mediafile.LastModifiedOrigin = "electron";
                                    mediafile.DateUpdated = DateTime.UtcNow;
                                    dbContext.Mediafiles.Update(mediafile);
                                }
                            };
                            break;

                        case "groupmemberships":
                            List<GroupMembership> grpmems = jsonApiDeSerializer.DeserializeList<GroupMembership>(data);
                            IEnumerable<GroupMembership> mygrpmems = grpmems.Where(gm => gm.GroupId == project.GroupId);
                            foreach(GroupMembership gm in mygrpmems)
                            {
                                GroupMembership grpmem = dbContext.Groupmemberships.Find(gm.Id);
                                if (grpmem.FontSize != gm.FontSize)
                                {
                                    if (grpmem.DateUpdated > sourceDate)
                                        report += GrpMemChangesReport(grpmem, gm);
                                    grpmem.FontSize = gm.FontSize;
                                    grpmem.LastModifiedBy = gm.LastModifiedBy;
                                    grpmem.LastModifiedOrigin = "electron";
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
                            IQueryable<PassageStateChange> mypscs = dbContext.Plans.Where(pl => pl.ProjectId == projectid).Join(dbContext.Sections, p => p.Id, s => s.PlanId, (p, s) => s).Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p).Join(pscs, p => p.Id, psc => psc.PassageId, (p, psc) => psc);
                            foreach(PassageStateChange psc in mypscs)
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
                int ret = await dbContext.SaveChangesNoTimestampAsync();

                return new FileResponse()
                {
                    Message = report,
                    FileURL = sFile,
                    Status = System.Net.HttpStatusCode.OK,
                    ContentType = ContentType,
                };
            }
            catch (Exception ex)
            {
                return new FileResponse()
                {
                    Message = ex.Message + (ex.InnerException != null && ex.InnerException.Message != "" ? "=>" + ex.InnerException.Message : ""),

                    FileURL = sFile,
                    Status = System.Net.HttpStatusCode.UnprocessableEntity,
                    ContentType = ContentType,
                };
            }
        }
    }
}
