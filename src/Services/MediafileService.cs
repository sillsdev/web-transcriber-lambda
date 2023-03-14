using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using TranscriberAPI.Utility.Extensions;
using static SIL.Transcriber.Utility.ResourceHelpers;

namespace SIL.Transcriber.Services
{
    public class MediafileService : BaseArchiveService<Mediafile>
    {
        private IS3Service S3service { get; }
        private PlanRepository PlanRepository { get; set; }
        private MediafileRepository MyRepository { get; set; }
        private readonly AppDbContext dbContext;
        readonly private HttpContext? HttpContext;
        public MediafileService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Mediafile> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            PlanRepository planRepository,
            IS3Service service,
            MediafileRepository myRepository,
                        IHttpContextAccessor httpContextAccessor,
            AppDbContextResolver contextResolver
        )
            : base(
                repositoryAccessor,
                queryLayerComposer,
                paginationContext,
                options,
                loggerFactory,
                request,
                resourceChangeTracker,
                resourceDefinitionAccessor,
                myRepository
            )
        {
            S3service = service;
            PlanRepository = planRepository;
            MyRepository = myRepository;
            HttpContext = httpContextAccessor.HttpContext;
            dbContext = (AppDbContext)contextResolver.GetContext();
        }

        public Mediafile? GetFromFile(int plan, string s3File)
        {
            IEnumerable<Mediafile> files = MyRepository.Get(); //bypass user check
            return files.SingleOrDefault(p => p.S3File == s3File && p.PlanId == plan);
        }
        public IEnumerable<Mediafile>? WBTUpdate()
        {
            HttpContext?.SetFP("api");
            return MyRepository.WBTUpdate();
        }
        private string DirectoryName(Plan? plan)
        {
            if (plan == null)
                return "";
            Project? proj = plan.Project;
            if (proj == null)
                proj = dbContext.Projects
                    .Where(p => p.Id == plan.ProjectId)
                    .Include(p => p.Organization)
                    .FirstOrDefault();
            Organization? org = proj?.Organization;
            if (org == null && proj?.OrganizationId != null)
                org = dbContext.Organizations
                    .Where(o => o.Id == proj.OrganizationId)
                    .FirstOrDefault();
            return org != null ? org.Slug + "/" + plan.Slug : throw new Exception("No org in DirectoryName");
        }

        public string DirectoryName(Mediafile entity)
        {
            int id = entity.Plan?.Id ?? entity.PlanId;
            /* this no longer works...project is null */
            Plan plan = dbContext.Plans
                .Include(p => p.Project)
                .ThenInclude(p => p.Organization)
                .Where(p => p.Id == id)
                .First();
            return plan != null ? DirectoryName(plan) : "";
        }

        public string? GetAudioUrl(Mediafile mf)
        {
            if (mf.S3File != null)
            {
                S3Response response = S3service.SignedUrlForPut(
                    mf.S3File,
                    DirectoryName(mf),
                    mf.ContentType ?? ""
                );
                return response.Message;
            }
            return null;
        }

        public async Task<string> GetNewFileNameAsync(Mediafile mf)
        {
            if (mf.SourceMedia == null && await S3service.FileExistsAsync(mf.OriginalFile ?? "", DirectoryName(mf)))
            {
                return Path.GetFileNameWithoutExtension(mf.OriginalFile)
                    + "__"
                    + Guid.NewGuid()
                    + Path.GetExtension(mf.OriginalFile);
            }
            else
            {
                return mf.OriginalFile ?? "";
            }
        }

        public IEnumerable<Mediafile> ReadyToSync(int PlanId, int artifactTypeId)
        {
            return MyRepository.ReadyToSync(PlanId, artifactTypeId);
        }
        public IEnumerable<Mediafile> PassageReadyToSync(int PassageId, int artifactTypeId)
        {
            return MyRepository.PassageReadyToSync(PassageId, artifactTypeId);
        }

        //I don't think we use this and I also don't think this will work now because edited fields
        //are ignored unless set in the definition OnWritingAsync
        public async Task<Mediafile?> CreateAsyncWithFile(Mediafile entity, IFormFile FileToUpload)
        {
            entity.S3File = await GetNewFileNameAsync(entity);
            S3Response response = await S3service.UploadFileAsync(
                FileToUpload,
                DirectoryName(entity)
            );
            entity.S3File = response.Message;
            entity.Filesize = FileToUpload.Length / 1024;
            entity.OriginalFile = FileToUpload.FileName;
            entity.ContentType = FileToUpload.ContentType;
            return await base.CreateAsync(entity, new CancellationToken());
        }

        public Mediafile? GetFileSignedUrlAsync(int id)
        {
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null)
                return null;
            if (mf.ResourcePassageId != null)
            {
                Mediafile? res = MyRepository.GetLatestShared((int)mf.ResourcePassageId);
                if (res != null && res.S3File != null)
                    mf.AudioUrl = S3service
                        .SignedUrlForGet(res.S3File, DirectoryName(res), res.ContentType ?? "")
                        .Message;
            }
            else
            {
                mf.AudioUrl = S3service
                    .SignedUrlForGet(mf.S3File ?? "", DirectoryName(mf), mf.ContentType ?? "")
                    .Message;
            }
            //_ = await UpdateAsync(id, mf, new CancellationToken());
            return mf;
        }

        public async Task<S3Response> GetFile(int id)
        {
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null || mf.S3File == null)
            {
                return new S3Response { Message = "", Status = HttpStatusCode.NotFound };
            }
            Plan? plan = PlanRepository.GetWithProject(mf.PlanId);
            if (
                mf.S3File.Length == 0
                || !(await S3service.FileExistsAsync(mf.S3File, DirectoryName(plan)))
            )
                return new S3Response
                {
                    Message = mf.S3File.Length > 0 ? mf.S3File : "",
                    Status = HttpStatusCode.NotFound
                };

            S3Response response = await S3service.ReadObjectDataAsync(
                mf.S3File ?? "",
                DirectoryName(plan)
            );
            response.Message = mf.OriginalFile ?? "";
            return response;
        }

        public async Task<S3Response> DeleteFile(int id)
        {
            //delete the s3 file
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null || mf.S3File == null)
            {
                return new S3Response { Message = "", Status = HttpStatusCode.NotFound };
            }
            Plan? plan = PlanRepository.GetWithProject(mf.PlanId);
            S3Response response = await S3service.RemoveFile(mf.S3File, DirectoryName(plan));
            return response;
        }

        public Mediafile? GetLatest(int passageId)
        {
            return MyRepository
                .Get()
                .Where(mf => mf.PassageId == passageId)
                .OrderByDescending(mf => mf.VersionNumber)
                .FirstOrDefault();
        }

        public string EAF(Mediafile mf)
        {
            string eaf = "";
            if (!string.IsNullOrEmpty(mf.Transcription))
            {
                //get the project language
                Plan? plan = PlanRepository.GetWithProject(mf.PlanId);
                string lang = !string.IsNullOrEmpty(plan?.Project.Language)
                    ? plan.Project.Language
                    : "en";
                string pattern = "([0-9]{1,2}:[0-9]{2}(:[0-9]{2})?)";

                eaf = LoadResource("EafTemplate.xml");
                XElement eafContent = XElement.Parse(eaf);
                //var sDebug = TraverseNodes(eafContent, 1);
                XElement elem;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                eafContent.Attribute("DATE").Value = DateTime.Now.ToUniversalTime().ToString();
                eafContent.GetElement("TIER").Attribute("DEFAULT_LOCALE").Value = lang;
                eafContent.GetElement("LOCALE").Attribute("LANGUAGE_CODE").Value = lang;
                eafContent.GetElement("HEADER").Attribute("MEDIA_FILE").Value = mf.S3File ?? "";
                eafContent.GetElement("MEDIA_DESCRIPTOR").Attribute("MEDIA_URL").Value =
                    mf.S3File ?? "";
                eafContent.GetElement("MEDIA_DESCRIPTOR").Attribute("MIME_TYPE").Value =
                    mf.ContentType ?? "";
                elem = eafContent.GetElementsWithAttribute("TIME_SLOT", "ts2").First();
                elem.Attribute("TIME_VALUE").Value = (mf.Duration ?? 0 * 1000).ToString();
                eafContent.GetElement("ANNOTATION_VALUE").Value = Regex.Replace(
                    HttpUtility.HtmlEncode(mf.Transcription),
                    pattern,
                    ""
                ); //TEST THE REGEX
                eaf = eafContent.ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }

            return eaf;
        }

        public async Task<Mediafile?> UpdateFileInfoAsync(int id, long filesize, decimal duration)
        {
            //where did you go?
            Mediafile? p = MyRepository.Get(id);
            if (p == null)
                return null;
            p.Filesize = filesize;
            p.Duration = (int)duration;
            _ = await base.UpdateArchivedAsync(id, p, new CancellationToken());
            return p;
        }

        public override async Task<Mediafile?> CreateAsync(
            Mediafile resource,
            CancellationToken cancellationToken
        )
        {
            //do this here because we know how to do it here...
            //but then capture it in the repository before it gets lost
            resource.S3File = GetNewFileNameAsync(resource).Result;
            resource.AudioUrl = GetAudioUrl(resource);
            return await base.CreateAsync(resource, cancellationToken);
        }
        public List<Mediafile> GetIPMedia(int organizationId)
        {
            IEnumerable<Intellectualproperty>? ip = dbContext.IntellectualPropertys.Where(ip => ip.OrganizationId == organizationId).ToList();
            return ip.Join(dbContext.Mediafiles, ip => ip.ReleaseMediafileId, m => m.Id, (ip, m) => m).ToList();
        }
    }
}
