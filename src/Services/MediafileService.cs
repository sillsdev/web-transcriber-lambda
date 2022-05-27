using System.Net;
using System.Text.RegularExpressions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ResourceHelpers;
using System.Xml.Linq;
using System.Web;
using TranscriberAPI.Utility.Extensions;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Services
{
    public class MediafileService : BaseArchiveService<Mediafile>
    {
        private IS3Service _S3service { get; }
        private PlanRepository PlanRepository { get; set; }
        private MediafileRepository MyRepository { get; set; }
        private readonly AppDbContext dbContext;
        public MediafileService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<Mediafile> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            PlanRepository planRepository,
            IS3Service service,
            MediafileRepository myRepository, AppDbContextResolver contextResolver) : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor, myRepository)
        {
            _S3service = service;
            PlanRepository = planRepository;
            MyRepository = myRepository;
            dbContext = (AppDbContext)contextResolver.GetContext();
        }

        public async Task<Mediafile?> GetFromFile(int plan, string s3File)
        {
            IEnumerable<Mediafile> files = await base.GetAsync(new CancellationToken());
            return files.SingleOrDefault(p => p.S3File == s3File && p.PlanId == plan);
        }

        private static string DirectoryName(Plan? plan)
        {
            if (plan == null) return "";
            return plan.Project.Organization.Slug + "/" + plan.Slug;
        }

        public string DirectoryName(Mediafile entity)
        {
            Plan? plan = PlanRepository.GetWithProject(entity.PlanId);
            if (plan != null)
                return DirectoryName(plan);
            return "";
        }

        public bool IsVernacularMedia(Mediafile mf)
        {
            return mf.ArtifactTypeId == null;
        }
        public async Task<string> GetNewFileNameAsync(Mediafile mf)
        {
            if (await _S3service.FileExistsAsync(mf.OriginalFile ?? "", DirectoryName(mf)))
            {
                return Path.GetFileNameWithoutExtension(mf.OriginalFile) + "__" + Guid.NewGuid() + Path.GetExtension(mf.OriginalFile);
            }
            else
            {
                return mf.OriginalFile??"";
            }
        }

        public override async Task<Mediafile?> CreateAsync(Mediafile entity, CancellationToken cancellationToken)
        {
            if (entity.VersionNumber == null) entity.VersionNumber = 1;
            if (entity.Link == null) entity.Link = false;
            if (entity.Transcriptionstate == null) entity.Transcriptionstate = "transcribeReady";
            if (entity.ResourcePassageId == null)
            {
                entity.S3File = await GetNewFileNameAsync(entity);
                S3Response response = _S3service.SignedUrlForPut(entity.S3File, DirectoryName(entity), entity.ContentType ?? "");
                entity.AudioUrl = response.Message;
                if (IsVernacularMedia(entity) && entity.PassageId != null)
                {
                    Mediafile? mfs = MyRepository.Get().ToList().Where(mf => mf.PassageId == entity.PassageId && IsVernacularMedia(mf) && !mf.Archived).OrderBy(m => m.VersionNumber).LastOrDefault();
                    if (mfs != null)
                    {
                        entity.VersionNumber = mfs.VersionNumber + 1;
                    }
                }
            } else
            {
                //pick the highest version media of the resource per passage
                Mediafile? sourcemediafile = MyRepository.Get().Where(x => x.PassageId == entity.ResourcePassageId && x.ReadyToShare && !x.Archived).OrderByDescending(m => m.VersionNumber).FirstOrDefault();
                entity.AudioUrl = sourcemediafile?.AudioUrl;
                entity.S3File = sourcemediafile?.S3File;
            }
            if (entity.PassageId != null && entity.EafUrl != null)
            {
                //create a passage state change with this info
                Passagestatechange psc = new ()
                {
                    PassageId = (int)entity.PassageId,
                    State = "",
                    Comments = entity.EafUrl
                };

                dbContext.Passagestatechanges.Add(psc);
                dbContext.SaveChanges();
                entity.EafUrl = "";
            }
            return await base.CreateAsync(entity, cancellationToken);
        }

        public IQueryable<Mediafile> ReadyToSync(int PlanId, int artifactTypeId)
        {
             return MyRepository.ReadyToSync(PlanId, artifactTypeId);
        }

        public async Task<Mediafile?> CreateAsyncWithFile(Mediafile entity, IFormFile FileToUpload)
        {
            entity.S3File = await GetNewFileNameAsync(entity);
            S3Response response = await _S3service.UploadFileAsync(FileToUpload, DirectoryName(entity));
            entity.S3File = response.Message;
            entity.Filesize = FileToUpload.Length / 1024;
            entity.OriginalFile = FileToUpload.FileName;
            entity.ContentType = FileToUpload.ContentType;
            return await base.CreateAsync(entity, new CancellationToken());
        }

        public async Task<Mediafile?> GetFileSignedUrlAsync(int id)
        {
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null) return null;
            if (mf.ResourcePassageId != null)
            {
                Mediafile? res = MyRepository.GetLatestShared((int)mf.ResourcePassageId);
                if (res != null && res.S3File != null)
                    mf.AudioUrl = _S3service.SignedUrlForGet(res.S3File, DirectoryName(res), res.ContentType ?? "").Message;
            }
            else
            {
                mf.AudioUrl = _S3service.SignedUrlForGet(mf.S3File ?? "", DirectoryName(mf), mf.ContentType ?? "").Message;
            }
            await UpdateAsync(id, mf, new CancellationToken());
            return mf;
        }

        public async Task<S3Response> GetFile(int id)
        {
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null || mf.S3File == null)
            {
                return new S3Response
                {
                    Message = "",
                    Status = HttpStatusCode.NotFound
                };
            }
            Plan? plan = PlanRepository.GetWithProject(mf.PlanId);
            if (mf.S3File.Length == 0 || !(await _S3service.FileExistsAsync(mf.S3File, DirectoryName(plan))))
                return new S3Response
                {
                    Message = mf.S3File.Length > 0 ? mf.S3File : "",
                    Status = HttpStatusCode.NotFound
                };

            S3Response response = await _S3service.ReadObjectDataAsync(mf.S3File??"", DirectoryName(plan));
            response.Message = mf.OriginalFile??"";
            return response;
        }
        public async Task<S3Response> DeleteFile(int id)
        {
            //delete the s3 file 
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null||mf.S3File == null)
            {
                return new S3Response
                {
                    Message = "",
                    Status = HttpStatusCode.NotFound
                };
            }
            Plan? plan = PlanRepository.GetWithProject(mf.PlanId);
            S3Response response = await _S3service.RemoveFile(mf.S3File, DirectoryName(plan));
            return response;
        }
        public Mediafile? GetLatest(int passageId)
        {
            return MyRepository.Get().Where(mf => mf.PassageId == passageId).OrderByDescending(mf => mf.VersionNumber).FirstOrDefault();
        }
        public string EAF(Mediafile mf)
        {
            string eaf = "";
            if (!string.IsNullOrEmpty(mf.Transcription))
            {
                //get the project language
                Plan? plan = PlanRepository.GetWithProject(mf.PlanId);
                string lang = !string.IsNullOrEmpty(plan?.Project.Language) ? plan.Project.Language : "en";
                string pattern = "([0-9]{1,2}:[0-9]{2}(:[0-9]{2})?)";

                eaf = LoadResource("EafTemplate.xml");
                XElement eafContent = XElement.Parse(eaf);
                //var sDebug = TraverseNodes(eafContent, 1);
                XElement elem;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                eafContent.Attribute("DATE").Value = DateTime.Now.ToUniversalTime().ToString();
                eafContent.GetElement("TIER").Attribute("DEFAULT_LOCALE").Value = lang;
                eafContent.GetElement("LOCALE").Attribute("LANGUAGE_CODE").Value = lang;
                eafContent.GetElement("HEADER").Attribute("MEDIA_FILE").Value = mf.S3File??"";
                eafContent.GetElement("MEDIA_DESCRIPTOR").Attribute("MEDIA_URL").Value = mf.S3File??"";
                eafContent.GetElement("MEDIA_DESCRIPTOR").Attribute("MIME_TYPE").Value = mf.ContentType??"";
                elem = eafContent.GetElementsWithAttribute("TIME_SLOT", "ts2").First();
                elem.Attribute("TIME_VALUE").Value = (mf.Duration??0 * 1000).ToString();
                eafContent.GetElement("ANNOTATION_VALUE").Value = Regex.Replace(HttpUtility.HtmlEncode(mf.Transcription), pattern, ""); //TEST THE REGEX
                eaf = eafContent.ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }

            return eaf;
        }
        public async Task<Mediafile?> UpdateFileInfoAsync(int id, long filesize, decimal duration)
        {
            //where did you go?
            Mediafile? p = MyRepository.Get(id);
            if (p == null) return null;
            p.Filesize = filesize;
            p.Duration = (int)duration;
            await base.UpdateAsync(id, p, new CancellationToken());
            return p;
        }
    }
}