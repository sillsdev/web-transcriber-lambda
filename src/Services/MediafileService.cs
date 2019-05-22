using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public class MediafileService : EntityResourceService<Mediafile>
    {
        private readonly IS3Service _S3service;

        private string PlanName(int id)
        {
            return "Plan" + id.ToString();
        }
        public MediafileService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<Mediafile> organizationRepository,
            ILoggerFactory loggerFactory,
            IS3Service service) : base(jsonApiContext, organizationRepository, loggerFactory)
        {
            _S3service = service;
        }
        public  async Task<Mediafile> CreateAsync(Mediafile entity, IFormFile FileToUpload)
        {

            S3Response response = await _S3service.UploadFileAsync(FileToUpload, PlanName(entity.PlanId));
            entity.AudioUrl = response.Message;
            entity.Filesize = FileToUpload.Length;
            entity.ContentType = FileToUpload.ContentType;
            entity = await base.CreateAsync(entity);
            return entity;
        }

        public async Task<S3Response> GetFile(int id)
        {
            Mediafile mf = await base.GetAsync(id);
            if (mf.AudioUrl.Length == 0 || !(await _S3service.FileExistsAsync(mf.AudioUrl, PlanName(mf.PlanId))))
                return null;

            S3Response response = await _S3service.ReadObjectDataAsync(mf.AudioUrl, PlanName(mf.PlanId));

            return response;
        }

        public override async Task<bool> DeleteAsync(int id)
        {
            Mediafile mf = await base.GetAsync(id);
            
            S3Response response = await _S3service.RemoveFile(mf.AudioUrl, PlanName(mf.PlanId));
            return await base.DeleteAsync(id);
        }        
    }
}
