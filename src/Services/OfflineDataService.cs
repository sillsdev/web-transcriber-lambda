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

namespace SIL.Transcriber.Services
{
    public class OfflineDataService: IOfflineDataService
    {
        protected readonly AppDbContext dbContext;
        protected readonly IJsonApiSerializer jsonApiSerializer;
        protected readonly MediafileService mediaService;
        private IS3Service _S3service;

        public OfflineDataService(IDbContextResolver contextResolver, IJsonApiSerializer jsonSer, MediafileService MediaService, IS3Service service)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            jsonApiSerializer = jsonSer;
            mediaService = MediaService;
            _S3service = service;
        }
        private DateTime AddCheckEntry(ZipArchive zipArchive)
        {
            var entry = zipArchive.CreateEntry("SILTranscriber", CompressionLevel.Fastest);
            var dt = DateTime.Now;
            using (StreamWriter sw = new StreamWriter(entry.Open()))
            {
                sw.WriteLine(dt.ToString());
            }
            return dt;
        }
        private void AddJsonEntry(ZipArchive zipArchive, string table, IList list, char sort)
        {
            var entry = zipArchive.CreateEntry("data/" + sort + "_" + table + ".json", CompressionLevel.Fastest);
            using (StreamWriter sw = new StreamWriter(entry.Open()))
            {
                //sw.WriteLine(JsonConvert.SerializeObject(list, Formatting.Indented));
                sw.WriteLine(jsonApiSerializer.Serialize(list));
            }
        }
        private void AddEafEntry(ZipArchive zipArchive, string name, string eafxml)
        {
            if (!String.IsNullOrEmpty(eafxml))
            {
                var entry = zipArchive.CreateEntry("media/" + Path.GetFileNameWithoutExtension(name) + ".eaf", CompressionLevel.Optimal);
                using (StreamWriter sw = new StreamWriter(entry.Open()))
                {
                    sw.WriteLine(eafxml);
                }
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
                AddJsonEntry(zipArchive, "groupmemberships", groups.Join(dbContext.Groupmemberships, g => g.Id, gm => gm.GroupId, (g, gm) => gm).ToList(), 'D');

                //projects
                AddJsonEntry(zipArchive, "projects", projects.ToList(), 'D');
                projects.ToList().ForEach(p => {
                    p.DateExported = exported;
                    dbContext.Projects.Update(p);
                });
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
            const string ExportFolder = "exports";
            string fileName = projectid != 0 ? string.Format("TranscriberProject_{0}.zip", projects.First().Slug) : string.Format("TranscriberOrg_{0}.zip", orgs.First().Slug);
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
    }
}
