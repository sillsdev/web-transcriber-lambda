using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class MediafileService : EntityResourceService<Mediafile>
    {
        private IS3Service _S3service { get; }
        private IOrganizationContext OrganizationContext { get; set; }
        private IJsonApiContext JsonApiContext { get; }
        private PlanRepository PlanRepository { get; set; }
        private MediafileRepository MediafileRepository { get; set; }

        private string DirectoryName(Plan plan)
        {
            return plan.Project.Organization.Slug + "/" + plan.Slug;
        }
        public MediafileService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<Mediafile> basemediafileRepository,
            PlanRepository planRepository,
            IOrganizationContext organizationContext,
            ILoggerFactory loggerFactory,
            IS3Service service) : base(jsonApiContext, basemediafileRepository, loggerFactory)
        {
            _S3service = service;
            OrganizationContext = organizationContext;
            JsonApiContext = jsonApiContext;
            PlanRepository = planRepository;
            MediafileRepository = (MediafileRepository)basemediafileRepository;
        }
        public override async Task<IEnumerable<Mediafile>> GetAsync()
        {
            return await GetScopedToOrganization<Mediafile>(
                base.GetAsync,
                OrganizationContext,
                JsonApiContext);

        }
        public override async Task<Mediafile> GetAsync(int id)
        {
            var files = await GetAsync();

            return files.SingleOrDefault(g => g.Id == id);
        }

        public  async Task<Mediafile> CreateAsync(Mediafile entity, IFormFile FileToUpload)
        {

            //var plan = await PlanRepository.GetAndIncludeAsync(entity.PlanId, "project");  //can add .organization
            var plan = PlanRepository.GetWithProject(entity.PlanId);  

            S3Response response = await _S3service.UploadFileAsync(FileToUpload, DirectoryName(plan));
            entity.AudioUrl = response.Message;
            entity.Filesize = FileToUpload.Length/1024;
            entity.OriginalFile = FileToUpload.FileName;
            entity.ContentType = FileToUpload.ContentType;
            entity = await base.CreateAsync(entity);
            return entity;
        }

        public async Task<S3Response> GetFile(int id)
        {
            Mediafile mf = MediafileRepository.GetInternal(id);
            var plan = PlanRepository.GetWithProject(mf.PlanId);
            if (mf.AudioUrl.Length == 0 || !(await _S3service.FileExistsAsync(mf.AudioUrl, DirectoryName(plan))))
                return null;

            S3Response response = await _S3service.ReadObjectDataAsync(mf.AudioUrl, DirectoryName(plan));

            return response;
        }

        public override async Task<bool> DeleteAsync(int id)
        {
            Mediafile mf = MediafileRepository.GetInternal(id);
            var plan = PlanRepository.GetWithProject(mf.PlanId);

            S3Response response = await _S3service.RemoveFile(mf.AudioUrl, DirectoryName(plan));
            return await base.DeleteAsync(id);
        }        
    }
}
