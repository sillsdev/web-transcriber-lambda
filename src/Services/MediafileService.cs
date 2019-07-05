﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
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
    public class MediafileService : BaseArchiveService<Mediafile>
    {
        private IS3Service _S3service { get; }
        private IOrganizationContext OrganizationContext { get; set; }
        private PlanRepository PlanRepository { get; set; }
        private MediafileRepository MediafileRepository { get; set; }

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
            PlanRepository = planRepository;
            MediafileRepository = (MediafileRepository)MyRepository; //from base
        }

        public override async Task<IEnumerable<Mediafile>> GetAsync()
        {
            return await GetScopedToCurrentUser(
             base.GetAsync,
             JsonApiContext);
/*            return await GetScopedToOrganization<Mediafile>(
                base.GetAsync,
                OrganizationContext,
                JsonApiContext); */

        }
        public override async Task<Mediafile> GetAsync(int id)
        {
            var files = await GetAsync();

            return files.SingleOrDefault(g => g.Id == id);
        }
        private string DirectoryName(Plan plan)
        {
            return plan.Project.Organization.Slug + "/" + plan.Slug;
        }

        private string DirectoryName(Mediafile entity)
        {
            var plan = PlanRepository.GetWithProject(entity.PlanId);
            return DirectoryName(plan);
        }

        //set the version number
        private void InitNewMediafile(Mediafile entity)
        {
            //aws versioning on
            //entity.S3File = entity.OriginalFile;
            entity.S3File = Guid.NewGuid() + "_" + entity.OriginalFile;

            var mfs = MediafileRepository.Get().Where(mf => mf.OriginalFile == entity.OriginalFile && mf.PlanId == entity.PlanId && !mf.Archived );
            if (mfs.Count() == 0)
                entity.VersionNumber = 1;
            else
            {
                var last = mfs.Where(mf => mf.Id == mfs.Max(m => m.Id)).First();
                entity.VersionNumber = last.VersionNumber + 1;
                entity.PassageId = last.PassageId;
            }
        }
        public override async Task<Mediafile> CreateAsync(Mediafile entity)
        {
            InitNewMediafile(entity); //set the version number
            var response = _S3service.PutSignedUrl(entity.S3File, DirectoryName(entity));
            entity.AudioUrl = response.Message;
            return await base.CreateAsync(entity);
        }
        public  async Task<Mediafile> CreateAsyncWithFile(Mediafile entity, IFormFile FileToUpload)
        {
           InitNewMediafile(entity);

            S3Response response = await _S3service.UploadFileAsync(FileToUpload, DirectoryName(entity));
            entity.S3File = response.Message;
            entity.Filesize = FileToUpload.Length/1024;
            entity.OriginalFile = FileToUpload.FileName;
            entity.ContentType = FileToUpload.ContentType;
            entity = await base.CreateAsync(entity);
            return entity;
        }

        public Mediafile GetFileSignedUrl(int id)
        {
            Mediafile mf = MediafileRepository.Get(id);
            mf.AudioUrl = _S3service.GetSignedUrl(mf.S3File, DirectoryName(mf)).Message;
            return mf;
        }

        public async Task<S3Response> GetFile(int id)
        {
            Mediafile mf = MediafileRepository.Get(id);
            var plan = PlanRepository.GetWithProject(mf.PlanId);
            if (mf.S3File.Length == 0 || !(await _S3service.FileExistsAsync(mf.S3File, DirectoryName(plan))))
                return null;

            S3Response response = await _S3service.ReadObjectDataAsync(mf.S3File, DirectoryName(plan));

            return response;
        }

        public override async Task<bool> DeleteAsync(int id)
        {
            Mediafile mf = MediafileRepository.Get(id);
            var plan = PlanRepository.GetWithProject(mf.PlanId);

            S3Response response = await _S3service.RemoveFile(mf.S3File, DirectoryName(plan));
            if (response.Status == HttpStatusCode.OK || response.Status == HttpStatusCode.NoContent)
                return await base.DeleteAsync(id);
            else
                throw new Exception(response.Message);
        }        
    }
}
