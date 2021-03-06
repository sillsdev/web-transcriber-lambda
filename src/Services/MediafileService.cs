﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;
using static SIL.Transcriber.Utility.ResourceHelpers;
using static SIL.Transcriber.Utility.ParatextHelpers;
using System.Xml.Linq;
using System.Web;
using TranscriberAPI.Utility.Extensions;

namespace SIL.Transcriber.Services
{
    public class MediafileService : BaseArchiveService<Mediafile>
    {
        private IS3Service _S3service { get; }
        private PlanRepository PlanRepository { get; set; }
        private MediafileRepository MediafileRepository { get; set; }
        private PassageService PassageService { get; set; }

        public MediafileService(
            IJsonApiContext jsonApiContext,
            MediafileRepository basemediafileRepository,
            PlanRepository planRepository,
            PassageService passageService,
            ILoggerFactory loggerFactory,
            IS3Service service) : base(jsonApiContext, basemediafileRepository, loggerFactory)
        {
            _S3service = service;
            PlanRepository = planRepository;
            PassageService = passageService;
            MediafileRepository = (MediafileRepository)MyRepository; //from base
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

        //set the version number
        private async Task InitNewMediafileAsync(Mediafile entity)
        {
            //aws versioning on
            //entity.S3File = entity.OriginalFile;
            entity.S3File = Path.GetFileNameWithoutExtension(entity.OriginalFile) + "__" + Guid.NewGuid() + Path.GetExtension(entity.OriginalFile);
            IQueryable<Mediafile> mfs = MediafileRepository.Get().Where(mf => mf.OriginalFile == entity.OriginalFile && mf.PlanId == entity.PlanId && !mf.Archived);
            if (mfs.Count() == 0)
                entity.VersionNumber = 1;
            else
            {
                Mediafile last = mfs.Where(mf => mf.Id == mfs.Max(m => m.Id)).First();
                entity.VersionNumber = last.VersionNumber + 1;
                entity.PassageId = last.PassageId;
                if (entity.PassageId != null)
                {
                    await PassageService.UpdateToReadyStateAsync((int)entity.PassageId);
                }
            }
        }
        public async Task<Mediafile> UpdateFileInfo(int id, long filesize, decimal duration)
        {
            Mediafile mf = MediafileRepository.Get(id);

            mf.Filesize = filesize / 1024;
            mf.Duration = (int)duration;
            await base.UpdateAsync(id, mf);
            return mf;
        }
        public override async Task<Mediafile> CreateAsync(Mediafile entity)
        {
            if (entity.PassageId == 0 || entity.PassageId is null)
            { //eh..hacky way to tell if we're uploading a new one, or copying one because it's being reopened
                await InitNewMediafileAsync(entity); //set the version number
                S3Response response = _S3service.SignedUrlForPut(entity.S3File, DirectoryName(entity), entity.ContentType);
                entity.AudioUrl = response.Message;
            }
            else
            {
                //find the latest and copy the s3file from it
                Mediafile mfs = MediafileRepository.Get().Where(mf => mf.PassageId == entity.PassageId && !mf.Archived).OrderBy(m => m.VersionNumber).LastOrDefault();
                entity.S3File = mfs.S3File;
            }
            return await base.CreateAsync(entity);
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
            await InitNewMediafileAsync(entity);
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
            mf.AudioUrl = _S3service.SignedUrlForGet(mf.S3File, DirectoryName(mf), mf.ContentType).Message;
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