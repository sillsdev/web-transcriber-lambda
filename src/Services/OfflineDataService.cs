using JsonApiDotNetCore.Data;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using SIL.Transcriber.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Collections;
using System;
using JsonApiDotNetCore.Serialization;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Diagnostics;

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

        public OfflineDataService(IDbContextResolver contextResolver, IJsonApiSerializer jsonSer, IJsonApiDeSerializer jsonDeser, MediafileService MediaService, IS3Service service)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            jsonApiSerializer = jsonSer;
            jsonApiDeSerializer = jsonDeser;
            mediaService = MediaService;
            _S3service = service;
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
            var entry = zipArchive.CreateEntry("SILTranscriber", CompressionLevel.Fastest);
            var dt = DateTime.UtcNow;
            WriteEntry(entry, dt.ToString("o"));
            return dt;
        }
        private void AddJsonEntry(ZipArchive zipArchive, string table, IList list, char sort)
        {
            var entry = zipArchive.CreateEntry("data/" + sort + "_" + table + ".json", CompressionLevel.Fastest);
            WriteEntry(entry, jsonApiSerializer.Serialize(list));
        }
        private void AddEafEntry(ZipArchive zipArchive, string name, string eafxml)
        {
            if (!String.IsNullOrEmpty(eafxml))
            {
                var entry = zipArchive.CreateEntry("media/" + Path.GetFileNameWithoutExtension(name) + ".eaf", CompressionLevel.Optimal);
                WriteEntry(entry, eafxml);
            }
        }
        private void AddStreamEntry(ZipArchive zipArchive, Stream fileStream, string dir, string newName)
        {
            var entry = zipArchive.CreateEntry(dir + newName, CompressionLevel.Optimal);
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

            using (var wc = new System.Net.WebClient())
                imageData = wc.DownloadData(url);

            return new MemoryStream(imageData);
        }
        private void AddOrgLogos(ZipArchive zipArchive, List<Organization> orgs)
        {
            orgs.ForEach(o =>
            {
                if (!String.IsNullOrEmpty(o.LogoUrl))
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
                if (!String.IsNullOrEmpty(u.avatarurl))
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
                if (!String.IsNullOrEmpty(m.S3File))
                {
                    var response = mediaService.GetFile(m.Id).Result;
                    AddStreamEntry(zipArchive, response.FileStream, "media/", m.S3File);
                    m.AudioUrl = "media/" + m.S3File;
                    AddEafEntry(zipArchive, m.S3File, m.EafUrl);
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
                    var fontfile = url.Substring(url.LastIndexOf("/") + 1);
                    url = bucket + fontfile;
                    AddStreamEntry(zipArchive, url, "fonts/", fontfile);
                    css = css.Substring(0, start + 1) + fontfile + css.Substring(end);
                }
                var entry = zipArchive.CreateEntry("fonts/" + cssfile, CompressionLevel.Fastest);
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
                projects = dbContext.Projects.Where(p => p.OrganizationId == orgid);

            var ms = new MemoryStream();
            using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                Dictionary<string, string> fonts = new Dictionary<string, string>();
                fonts.Add("Charis SIL", "");
                var exported= AddCheckEntry(zipArchive);

                var orgmems = dbContext.Organizationmemberships.Where(om => om.OrganizationId == orgid);
                
                //users
                var userList = orgmems.Join(dbContext.Users, om => om.UserId, u => u.Id, (om, u) => u).ToList();
                AddUserAvatars(zipArchive, userList);
                AddJsonEntry(zipArchive, "users", userList, 'A');

                //org
                var orgList = orgs.ToList();
                AddOrgLogos(zipArchive, orgList);
                AddJsonEntry(zipArchive, "organizations", orgList, 'B');

                //organizationmemberships
                AddJsonEntry(zipArchive, "organizationmemberships", orgmems.ToList(), 'C');

                //groups
                var groups = dbContext.Groups.Where(om => om.OwnerId == orgid);
                AddJsonEntry(zipArchive, "groups", groups.ToList(), 'C');

                //groupmemberships
                var gms = groups.Join(dbContext.Groupmemberships, g => g.Id, gm => gm.GroupId, (g, gm) => gm).ToList();
                AddJsonEntry(zipArchive, "groupmemberships", gms, 'D');
                foreach( string font in gms.Where(gm => gm.Font != null).Select(gm => gm.Font))
                {
                    fonts[font] = ""; //add it if it's not there
                }
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
                AddJsonEntry(zipArchive, "projectintegrations", projects.Join(dbContext.Projectintegrations, p => p.Id, pi => pi.ProjectId, (p, pi) => pi).ToList(), 'E');
                //plans
                var plans = projects.Join(dbContext.Plans, p => p.Id, pl => pl.ProjectId, (p, pl) => pl);
                AddJsonEntry(zipArchive, "plans", plans.ToList(), 'E');
                //sections
                var sections = plans.Join(dbContext.Sections, p => p.Id, s => s.PlanId, (p, s) => s);
                AddJsonEntry(zipArchive, "sections", sections.ToList(), 'F');
                //passagesections
                var passagesections = sections.Join(dbContext.Passagesections, s => s.Id, ps => ps.SectionId, (s, ps) => ps);
                AddJsonEntry(zipArchive, "passagesections", passagesections.ToList(), 'H');
                //passages
                var passages = passagesections.Join(dbContext.Passages, ps => ps.PassageId, p => p.Id, (ps, p) => p);
                AddJsonEntry(zipArchive, "passages", passages.ToList(), 'G');
                //mediafiles
                var mediafiles = plans.Join(dbContext.Mediafiles, p => p.Id, m => m.PlanId, (p, m) => m);
                var mediaList = mediafiles.ToList();
                AddMedia(zipArchive, mediaList);
                AddJsonEntry(zipArchive, "mediafiles", mediaList, 'H');
                //passagestatechange
                var passagestatechanges = passages.Join(dbContext.Passagestatechanges, p => p.Id, psc => psc.PassageId, (p, psc) => psc);
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
            const string ContentType = "application/zip";
            string fileName = projectid != 0 ? string.Format("Transcriber_{0}.ptf", projects.First().Slug) : string.Format("TranscriberOrg_{0}.ptf", orgs.First().Slug);
            var s3response = _S3service.UploadFileAsync(ms, true, ContentType, fileName, ExportFolder).Result;
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
            const string ContentType = "application/zip";
            // Project project = dbContext.Projects.Where(p => p.Id == id).First();
            string fileName = string.Format("{0}_{1}.zip", Path.GetFileNameWithoutExtension(sFile), DateTime.Now.Ticks);
            //get a signedurl for it now
            return new FileResponse()
            {
                Message = fileName,
                FileURL = _S3service.SignedUrlForPut(fileName, ImportFolder, ContentType).Message,
                Status = System.Net.HttpStatusCode.OK,
                ContentType = ContentType,
            };
        }
        
        public async Task<FileResponse> ImportFileAsync(string sFile)
        {
            const string ContentType = "application/zip";
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
            try {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!entry.FullName.StartsWith("data"))
                        continue;
                    string data = new StreamReader(entry.Open()).ReadToEnd();
                    data = "{data: " + data + "}";
                    string name = Path.GetFileNameWithoutExtension(entry.Name.Substring(2));
                    switch (name)
                    {
                        case "users":
                            List<User> users = jsonApiDeSerializer.DeserializeList<User>(data);
                            users.ForEach(u =>
                            {
                                var user = dbContext.Users.Find(u.Id);
                                if (user.DateUpdated > sourceDate)
                                {
                                    /* simplify the object to report on */
                                    report += "User" + Environment.NewLine + JsonConvert.SerializeObject(new { user.Id, user.Email, user.FamilyName, user.GivenName, user.Name, user.Phone, user.Locale, user.Timezone, user.NewsPreference,  user.DigestPreference }, Formatting.Indented) + Environment.NewLine;
                                }
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
                                user.DateUpdated = DateTime.UtcNow; // u.DateUpdated;
                                /* TODO: figure out if the avatar needs uploading */
                                dbContext.Users.Update(user);
                            });
                            break;

                        case "sections":
                            List<Section> sections = jsonApiDeSerializer.DeserializeList<Section>(data);
                            sections.ForEach(s =>
                            {
                                var section = dbContext.Sections.Find(s.Id);
                                if (section.DateUpdated > sourceDate)
                                {
                                    /* simplify the object to report on */
                                    report += "Section" + Environment.NewLine + JsonConvert.SerializeObject(new {section.Id, section.PlanId, section.Plan, section.Sequencenum, section.ReviewerId, section.Reviewer, section.TranscriberId, section.Transcriber}) + Environment.NewLine;
                                }
                                section.ReviewerId = s.ReviewerId;
                                section.TranscriberId = s.TranscriberId;
                                section.State = s.State;
                                section.LastModifiedBy = s.LastModifiedBy;
                                section.LastModifiedOrigin = "electron";
                                section.DateUpdated = DateTime.UtcNow; // s.DateUpdated;
                                dbContext.Sections.Update(section);
                            });
                            break;

                        case "passages":
                            List<Passage> passages = jsonApiDeSerializer.DeserializeList<Passage>(data);
                            passages.ForEach(p =>
                            {
                                var passage = dbContext.Passages.Find(p.Id);
                                if (passage.State != p.State)
                                {
                                    if (passage.DateUpdated > sourceDate)
                                    {
                                        /* simplify the object to report on */
                                        report += "Passage" + Environment.NewLine + JsonConvert.SerializeObject(new { passage.Id, passage.Sequencenum, passage.Reference, passage.State }) + Environment.NewLine;
                                    }
                                    passage.State = p.State;
                                    passage.LastModifiedBy = p.LastModifiedBy;
                                    passage.LastModifiedOrigin = "electron";
                                    passage.DateUpdated = DateTime.UtcNow; // p.DateUpdated;
                                    dbContext.Passages.Update(passage);
                                }
                            });
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
                            mediafiles.ForEach(m =>
                            {
                                var mediafile = dbContext.Mediafiles.Find(m.Id);
                                if (mediafile.Transcription != m.Transcription || mediafile.Position != m.Position)
                                {
                                    if (mediafile.DateUpdated > sourceDate)
                                    {
                                        /* simplify the object to report on */
                                        /* TODO? get the passage info */
                                        report += "Mediafile" + Environment.NewLine + JsonConvert.SerializeObject(new { mediafile.Id, mediafile.OriginalFile, mediafile.Position, mediafile.Transcription }) + Environment.NewLine;
                                    }
                                    mediafile.Position = m.Position;
                                    mediafile.Transcription = m.Transcription;
                                    mediafile.LastModifiedBy = m.LastModifiedBy;
                                    mediafile.LastModifiedOrigin = "electron";
                                    mediafile.DateUpdated = DateTime.UtcNow; //  m.DateUpdated;
                                }
                            });
                            break;

                        case "groupmemberships":
                            List<GroupMembership> grpmems = jsonApiDeSerializer.DeserializeList<GroupMembership>(data);
                            grpmems.ForEach(gm =>
                            {
                                var grpmem = dbContext.Groupmemberships.Find(gm.Id);
                                if (grpmem.FontSize != gm.FontSize)
                                {
                                    if (grpmem.DateUpdated > sourceDate)
                                    {
                                        /* TODO? get the group and user info */
                                        report += "GroupMembership" + Environment.NewLine + JsonConvert.SerializeObject(new { grpmem.Id, grpmem.GroupId, grpmem.Group, grpmem.FontSize }) + Environment.NewLine;
                                    }
                                    grpmem.FontSize = gm.FontSize;
                                    grpmem.LastModifiedOrigin = "electron";
                                    grpmem.DateUpdated = DateTime.UtcNow;
                                }
                            });
                            break;

                        /*  Local changes to project integrations should just stay local
                        case "projectintegrations":
                            List<ProjectIntegration> pis = jsonApiDeSerializer.DeserializeList<ProjectIntegration>(data);
                            break;
                        */

                        case "passagestatechanges":
                            List<PassageStateChange> pscs = jsonApiDeSerializer.DeserializeList<PassageStateChange>(data);
                            pscs.ForEach(psc =>
                            {
                                psc.DateUpdated = DateTime.UtcNow;
                                dbContext.Passagestatechanges.Update(psc);
                            });
                            break;
                    }
                }
                var ret = await dbContext.SaveChangesNoTimestampAsync();

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
                Console.WriteLine(ex);
                return new FileResponse()
                {
                    Message = ex.Message,
                    FileURL = sFile,
                    Status = System.Net.HttpStatusCode.UnprocessableEntity,
                    ContentType = ContentType,
                };
            }
        }
    }
}
