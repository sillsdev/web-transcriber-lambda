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

namespace SIL.Transcriber.Services
{
    public class OfflineDataService: IOfflineDataService
    {
        protected readonly AppDbContext dbContext;
        protected readonly IJsonApiSerializer jsonApiSerializer;
        protected readonly MediafileService mediaService;

        public OfflineDataService(IDbContextResolver contextResolver, IJsonApiSerializer jsonSer, MediafileService MediaService)//, OrganizationRepository organizationRepository, ProjectRepository projectRepository)
        {
           // OrganizationRepository = organizationRepository;
            //ProjectRepository = projectRepository;
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            jsonApiSerializer = jsonSer;
            mediaService = MediaService;
        }
        private void AddJsonEntry(ZipArchive zipArchive, string table, IList list)
        {
            var entry = zipArchive.CreateEntry(table + ".json", CompressionLevel.Fastest);
            using (StreamWriter sw = new StreamWriter(entry.Open()))
            {
                //sw.WriteLine(JsonConvert.SerializeObject(list, Formatting.Indented));
                sw.WriteLine(jsonApiSerializer.Serialize(list));
            }
        }
        private void AddEafEntry(ZipArchive zipArchive, string name, string eafxml)
        {
            var entry = zipArchive.CreateEntry("media/" + name + ".eaf", CompressionLevel.Optimal);
            using (StreamWriter sw = new StreamWriter(entry.Open()))
            {
                sw.WriteLine(eafxml);
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
            media.ForEach(m =>
            {
                if (!String.IsNullOrEmpty(m.S3File))
                {
                    var response = mediaService.GetFile(m.Id).Result;
                    AddStreamEntry(zipArchive, response.FileStream, "media/", m.S3File);
                    m.AudioUrl = "media/" + m.S3File;
                    AddEafEntry(zipArchive, m.S3File, mediaService.EAF(m.Id));
                }
            });
        }
        private FileResponse Export(int orgid, int projectid = 0)
        {
            var response = new FileResponse();
            //export this organization
            //Organization org = OrganizationRepository.Get().Where(o => o.Id == orgid).FirstOrDefaultAsync().Result;
            IQueryable<Organization> orgs = dbContext.Organizations.Where(o => o.Id == orgid);
            IQueryable<Project> projects;
            if (orgs.Count() == 0)
            {
                response.Status = System.Net.HttpStatusCode.NotFound;
                response.Message = "Organization does not exist. " + orgid.ToString();
                return response;
            }

            if (projectid != 0)
                projects = dbContext.Projects.Where(p => p.Id == projectid);
            else
                projects = dbContext.Projects.Where(p => p.OrganizationId == orgid);

            var ms = new MemoryStream();
            using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var orgList = orgs.ToList();
                AddOrgLogos(zipArchive, orgList);
                AddJsonEntry(zipArchive, "organizations", orgList);

                //organizationmemberships
                var orgmems = dbContext.Organizationmemberships.Where(om => om.OrganizationId == orgid);
                AddJsonEntry(zipArchive, "organizationmemberships", orgmems.ToList());
                //users
                var userList = orgmems.Join(dbContext.Users, om => om.UserId, u => u.Id, (om, u) => u).ToList();
                AddUserAvatars(zipArchive, userList);
                AddJsonEntry(zipArchive, "users",userList);
                
                //groups
                var groups = dbContext.Groups.Where(om => om.OwnerId == orgid);
                AddJsonEntry(zipArchive, "groups", groups.ToList());
                //groupmemberships
                AddJsonEntry(zipArchive, "groupmemberships", groups.Join(dbContext.Groupmemberships, g => g.Id, gm => gm.GroupId, (g, gm) => gm).ToList());
                //projects
                AddJsonEntry(zipArchive, "projects", projects.ToList());

                //projectintegrations
                AddJsonEntry(zipArchive, "projectintegrations", projects.Join(dbContext.Projectintegrations, p => p.Id, pi => pi.ProjectId, (p, pi) => pi).ToList());
                //plans
                var plans = projects.Join(dbContext.Plans, p => p.Id, pl => pl.ProjectId, (p, pl) => pl);
                AddJsonEntry(zipArchive, "plans", plans.ToList());
                //sections
                var sections = plans.Join(dbContext.Sections, p => p.Id, s => s.PlanId, (p, s) => s);
                AddJsonEntry(zipArchive, "sections", sections.ToList());
                //passagesections
                var passagesections = sections.Join(dbContext.Passagesections, s => s.Id, ps => ps.SectionId, (s, ps) => ps);
                AddJsonEntry(zipArchive, "passagesections", passagesections.ToList());
                //passages
                var passages = passagesections.Join(dbContext.Passages, ps => ps.PassageId, p => p.Id, (ps, p) => p);
                AddJsonEntry(zipArchive, "passages", passages.ToList());
                //mediafiles
                var mediafiles = plans.Join(dbContext.Mediafiles, p => p.Id, m => m.PlanId, (p, m) => m);
                var mediaList = mediafiles.ToList();
                AddMedia(zipArchive, mediaList);
                AddJsonEntry(zipArchive, "mediafiles", mediaList);

                //ALL
                //activitystates
                AddJsonEntry(zipArchive, "activitystates", dbContext.Activitystates.ToList());
                //integrations
                AddJsonEntry(zipArchive, "integrations", dbContext.Integrations.ToList());
                //projecttypes
                AddJsonEntry(zipArchive, "projecttypes", dbContext.Projecttypes.ToList());
                //plantypes
                AddJsonEntry(zipArchive, "plantypes", dbContext.Plantypes.ToList());
                //roles
                AddJsonEntry(zipArchive, "roles", dbContext.Roles.ToList());
               

            }
            ms.Position = 0;
            response.ContentType = "application/zip";
            response.Status = System.Net.HttpStatusCode.OK;
            response.FileStream = ms;
            if (projectid != 0)
            {
                response.Message = string.Format("TranscriberProject_{0}.zip", projects.First().Slug);

            }
            else
            {
                response.Message = string.Format("TranscriberOrg_{0}.zip", orgs.First().Slug);

            }
            return response;
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
