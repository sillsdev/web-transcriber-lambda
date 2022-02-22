using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;
using static SIL.Transcriber.Utility.ResourceHelpers;
using System.Xml.Linq;
using System.Web;
using TranscriberAPI.Utility.Extensions;
using SIL.Transcriber.Data;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{
    public class MediafileService : BaseArchiveService<Mediafile>
    {
        private IS3Service _S3service { get; }
        private PlanRepository PlanRepository { get; set; }
        private MediafileRepository MediafileRepository { get; set; }
        private PassageService PassageService { get; set; }
        private PassageStateChangeService PSCService { get; set; }
        protected readonly AppDbContext dbContext;
        private HttpContext HttpContext;

        public MediafileService(AppDbContextResolver contextResolver,
            IJsonApiContext jsonApiContext,
            MediafileRepository basemediafileRepository,
            PlanRepository planRepository,
            PassageService passageService,
            PassageStateChangeService pscService,
            ILoggerFactory loggerFactory,
            IS3Service service,           
            IHttpContextAccessor httpContextAccessor) : base(jsonApiContext, basemediafileRepository, loggerFactory)
        {
            _S3service = service;
            PlanRepository = planRepository;
            PassageService = passageService;
            PSCService = pscService;
            MediafileRepository = (MediafileRepository)MyRepository; //from base
            dbContext = (AppDbContext)contextResolver.GetContext();
            HttpContext = httpContextAccessor.HttpContext;
        }

        public override async Task<IEnumerable<Mediafile>> GetAsync()
        {
            return await GetScopedToCurrentUser(
             base.GetAsync,
             JsonApiContext);
        }
        public override async Task<Mediafile> GetAsync(int id)
        {
            IEnumerable<Mediafile> files = await GetAsync();

            return files.SingleOrDefault(g => g.Id == id);
        }
        public async Task<Mediafile> GetFromFile(string s3File)
        {
            IEnumerable<Mediafile> files = await base.GetAsync();
            return files.SingleOrDefault(p => p.S3File == s3File);
        }

        private string DirectoryName(Plan plan)
        {
            return plan.Project.Organization.Slug + "/" + plan.Slug;
        }

        public string DirectoryName(Mediafile entity)
        {
            Plan plan = PlanRepository.GetWithProject(entity.PlanId);
            return DirectoryName(plan);
        }

        
        public async Task<Mediafile> UpdateFileInfo(int id, long filesize, decimal duration)
        {
            Mediafile mf = MediafileRepository.Get(id);

            mf.Filesize = filesize / 1024;
            mf.Duration = (int)duration;
            await base.UpdateAsync(id, mf);
            return mf;
        }
        private int VernacularId(AppDbContext dbContext)
        {
            int vernacularId = 0;
            ArtifactType vernacular = dbContext.Artifacttypes.Where(at => at.Typename == "vernacular").FirstOrDefault();
            if (vernacular != null) vernacularId = vernacular.Id;
            return vernacularId;
        }
        public bool IsVernacularMedia(Mediafile mf)
        {
            return mf.ArtifactTypeId == null; // This starts another thread on the context|| mf.ArtifactTypeId == VernacularId(dbContext);
        }
        public async Task<string> GetNewFileNameAsync(Mediafile mf)
        {
            if (await _S3service.FileExistsAsync(mf.OriginalFile, DirectoryName(mf)))
            {
                return Path.GetFileNameWithoutExtension(mf.OriginalFile) + "__" + Guid.NewGuid() + Path.GetExtension(mf.OriginalFile);
            }
            else
            {
                return mf.OriginalFile;
            }
        }

        public override async Task<Mediafile> CreateAsync(Mediafile entity)
        {
            if (entity.VersionNumber == null) entity.VersionNumber = 1;
            if (entity.Link == null) entity.Link = false;
            if (entity.Transcriptionstate == null) entity.Transcriptionstate = "transcribeReady";
            if (entity.ResourcePassageId == null)
            {
                entity.S3File = await GetNewFileNameAsync(entity);
                S3Response response = _S3service.SignedUrlForPut(entity.S3File, DirectoryName(entity), entity.ContentType);
                entity.AudioUrl = response.Message;
                if (IsVernacularMedia(entity) && entity.PassageId != null)
                {
                    Mediafile mfs = MediafileRepository.Get().Where(mf => mf.PassageId == entity.PassageId && IsVernacularMedia(mf) && !mf.Archived).OrderBy(m => m.VersionNumber).LastOrDefault();
                    if (mfs != null)
                    {
                        entity.VersionNumber = mfs.VersionNumber + 1;
                    }
                }
            } else
            {
                //pick the highest version media of the resource per passage
                Mediafile sourcemediafile = MediafileRepository.Get().Where(x => x.PassageId == entity.ResourcePassageId && x.ReadyToShare && !x.Archived).OrderByDescending(m => m.VersionNumber).FirstOrDefault();
                entity.AudioUrl = sourcemediafile.AudioUrl;
                entity.S3File = sourcemediafile.S3File;
            }
            if (entity.PassageId != null && entity.EafUrl != null)
            {
                //create a passage state change with this info
                await PSCService.CreateAsync(new PassageStateChange
                {
                    PassageId = (int)entity.PassageId,
                    State = "",
                    Comments = entity.EafUrl
                });
                entity.EafUrl = "";
            }
            return await base.CreateAsync(entity);
        }
        public async Task<Mediafile> UpdateToReadyStateAsync(int id)
        {
            Mediafile p = await MediafileRepository.GetAsync(id);
            p.Transcriptionstate = "transcribeReady";
            string fp = HttpContext.GetFP();
            HttpContext.SetFP("api");  //even the guy who sent this needs these changes
            await base.UpdateAsync(id, p);
            HttpContext.SetFP(fp);
            return p;
        }
        public IQueryable<Mediafile> ReadyToSync(int PlanId, int artifactTypeId)
        {
             return MediafileRepository.ReadyToSync(PlanId, artifactTypeId);
        }
        /*
        public override async Task<Mediafile> UpdateAsync(int id, Mediafile media)
        {
            Mediafile mf = MediafileRepository.Get(id);
            //if the transcription has changed...update the eaf
            if (JsonApiContext.AttributesToUpdate.Any(kvp=>kvp.Key.PublicAttributeName == "transcription"))
            {
                mf.Transcription = media.Transcription;
                var contextEntity = JsonApiContext.ResourceGraph.GetContextEntity("mediafiles");
                JsonApiContext.AttributesToUpdate[contextEntity.Attributes.Where(a => a.PublicAttributeName == "eaf-url").First()] = EAF(mf);
            }
            mf = await base.UpdateAsync(id, media);
            return mf;
        }
        */
        public async Task<Mediafile> CreateAsyncWithFile(Mediafile entity, IFormFile FileToUpload)
        {
            entity.S3File = await GetNewFileNameAsync(entity);
            S3Response response = await _S3service.UploadFileAsync(FileToUpload, DirectoryName(entity));
            entity.S3File = response.Message;
            entity.Filesize = FileToUpload.Length / 1024;
            entity.OriginalFile = FileToUpload.FileName;
            entity.ContentType = FileToUpload.ContentType;
            entity = await base.CreateAsync(entity);
            return entity;
        }

        public async Task<Mediafile> GetFileSignedUrlAsync(int id)
        {
            Mediafile mf = MediafileRepository.Get(id);
            if (mf.ResourcePassageId != null)
            {
                Mediafile res = MediafileRepository.GetLatestShared((int)mf.ResourcePassageId);
                mf.AudioUrl = _S3service.SignedUrlForGet(res.S3File, DirectoryName(res), res.ContentType).Message;
            }
            else
            {
                mf.AudioUrl = _S3service.SignedUrlForGet(mf.S3File, DirectoryName(mf), mf.ContentType).Message;
            }
            await MediafileRepository.UpdateAsync(id, mf);
            return mf;
        }

        public async Task<S3Response> GetFile(int id)
        {
            Mediafile mf = MediafileRepository.Get(id);
            Plan plan = PlanRepository.GetWithProject(mf.PlanId);
            if (mf.S3File.Length == 0 || !(await _S3service.FileExistsAsync(mf.S3File, DirectoryName(plan))))
                return new S3Response
                {
                    Message = mf.S3File.Length > 0 ? mf.S3File : "",
                    Status = HttpStatusCode.NotFound
                };

            S3Response response = await _S3service.ReadObjectDataAsync(mf.S3File, DirectoryName(plan));
            response.Message = mf.OriginalFile;
            return response;
        }
        public async Task<S3Response> DeleteFile(int id)
        {
            //delete the s3 file 
            Mediafile mf = MediafileRepository.Get(id);
            Plan plan = PlanRepository.GetWithProject(mf.PlanId);
            S3Response response = await _S3service.RemoveFile(mf.S3File, DirectoryName(plan));
            return response;
        }
        public Mediafile GetLatest(int passageId)
        {
            return MediafileRepository.Get().Where(mf => mf.PassageId == passageId).OrderByDescending(mf => mf.VersionNumber).FirstOrDefault();
        }
        public string EAF(Mediafile mf)
        {
            string eaf = "";
            if (!string.IsNullOrEmpty(mf.Transcription))
            {
                //get the project language
                Plan plan = PlanRepository.GetWithProject(mf.PlanId);
                string lang = !string.IsNullOrEmpty(plan.Project.Language) ? plan.Project.Language : "en";
                string pattern = "([0-9]{1,2}:[0-9]{2}(:[0-9]{2})?)";

                eaf = LoadResource("EafTemplate.xml");
                XElement eafContent = XElement.Parse(eaf);
                //var sDebug = TraverseNodes(eafContent, 1);
                XElement elem;
                eafContent.Attribute("DATE").Value = DateTime.Now.ToString();
                eafContent.GetElement("TIER").Attribute("DEFAULT_LOCALE").Value = lang;
                eafContent.GetElement("LOCALE").Attribute("LANGUAGE_CODE").Value = lang;
                eafContent.GetElement("HEADER").Attribute("MEDIA_FILE").Value = mf.S3File;
                eafContent.GetElement("MEDIA_DESCRIPTOR").Attribute("MEDIA_URL").Value = mf.S3File;
                eafContent.GetElement("MEDIA_DESCRIPTOR").Attribute("MIME_TYPE").Value = mf.ContentType;
                elem = eafContent.GetElementsWithAttribute("TIME_SLOT", "ts2").First();
                elem.Attribute("TIME_VALUE").Value = (mf.Duration * 1000).ToString();
                eafContent.GetElement("ANNOTATION_VALUE").Value = Regex.Replace(HttpUtility.HtmlEncode(mf.Transcription), pattern, ""); //TEST THE REGEX
                eaf = eafContent.ToString();
            }

            return eaf;
        }
    }
}