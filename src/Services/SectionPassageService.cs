using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using SIL.Transcriber.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Serialization.Objects;

using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using System.Threading;

namespace SIL.Transcriber.Services
{
    public class SectionPassageService : JsonApiResourceService<SectionPassage, int>
    {
        protected SectionPassageRepository MyRepository { get; }
        //protected IJsonApiOptions options { get; }
        protected ILogger<SectionPassage> Logger { get; set; }
        public SectionPassageService(
            IResourceRepositoryAccessor repositoryAccessor, 
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, 
            IJsonApiOptions options, 
            ILoggerFactory loggerFactory,
            IJsonApiRequest request, 
            IResourceChangeTracker<SectionPassage> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            SectionPassageRepository myRepository) 
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request,resourceChangeTracker, resourceDefinitionAccessor)
        {
            this.MyRepository = myRepository;
            this.Logger = loggerFactory.CreateLogger<SectionPassage>();
        }
        public override async Task<SectionPassage?> GetAsync(int id, CancellationToken cancelled)
        {
            SectionPassage entity = await base.GetAsync(id, cancelled); // dbContext.Sectionpassages.Where(e => e.Id == id).FirstOrDefault();
            if (entity != null && entity.Complete)
                return entity;
            return null;
        }
        
        public async Task<SectionPassage?>? PostAsync(SectionPassage entity)
        {
            object? input = entity.Data != null ? JsonConvert.DeserializeObject(entity.Data) : null;
            
            if (input==null || !input.GetType().IsAssignableFrom(typeof(JArray))) throw new Exception("Invalid input");

            JArray data = (JArray)input;

            if (data.Count == 0) return entity;

            SectionPassage? inprogress = MyRepository.GetByUUID(entity.Uuid) ;
            if (inprogress != null)
            {
                if (inprogress.Complete)
                {
                    /* another call completed successfully, so call off future retries */
                    return entity;
                }
                else
                {
                    /* another call is in progress...but in case it fails and we need a retry, fail this one */
                    return null; // throw new JsonApiException(new Error(502,"orbit is dumb"));
                }
            }
            entity.DateCreated = DateTime.UtcNow;
            try
            {
                await CreateAsync(entity, new CancellationToken());
            }
            catch (Exception ex)
            {
                Logger.LogError($"{ex}");
                if (ex.InnerException != null && ex.InnerException.Message.Contains("23505"))
                    return null;
            }
            using IDbContextTransaction transaction = MyRepository.BeginTransaction();
            try
            {
                IEnumerable<JToken> updsecs = data.Where(d => ((bool?)d[0]?["issection"]??false) && ((bool?)d[0]?["changed"]??false));

                //add all sections
                List<Section> updsections = new List<Section>();

                foreach (JArray item in updsecs)
                {
                    updsections.Add((item[0]?["id"]??"").ToString() != "" ? MyRepository.GetSection((int?)item[0]["id"]??0).UpdateFrom(item[0]) : new Section().UpdateFrom(item[0], entity.PlanId));
                }
                if (updsections.Count > 0)
                {
                    MyRepository.BulkUpdateSections(updsections);
                    int ix = 0;
                    foreach (JArray item in updsecs)
                    {
                        item[0]["id"] = updsections[ix].Id;
                        ix++;
                    }
                }
                int lastSectionId = 0;
                /* process all the passages now */
                List<JToken> updpass = new List<JToken>();
                List<Passage> updpassages = new List<Passage>();
                List<Passage> delpassages = new List<Passage>();
                foreach (JArray item in data)
                {
                    if ((bool)item[0]["issection"])
                    {
                        if (item[0]["id"] != null && item[0]["id"].ToString() != "")  //saving in chunks may not have saved this section...passages will be marked unchanged
                        {
                            lastSectionId = (int)item[0]["id"];
                            if (item.Count > 1)
                            {
                                if ((bool)item[1]["changed"])
                                {
                                    updpass.Add(item);
                                    updpassages.Add(item[1]["id"] != null && item[1]["id"].ToString() != "" ? MyRepository.GetPassage((int)item[1]["id"]).UpdateFrom(item[1]) : new Passage().UpdateFrom(item[1], lastSectionId));
                                }
                                else if (item[1]["deleted"] != null && (bool)item[1]["deleted"])
                                {
                                    delpassages.Add(MyRepository.GetPassage((int)item[1]["id"]).UpdateFrom(item[1]));
                                }
                            }
                        }
                    }
                    else if ((bool?)item[0]["changed"]??false)
                    {
                        updpass.Add(item);
                        updpassages.Add((item[0]?["id"]?.ToString() ?? "") != "" ? MyRepository.GetPassage((int)item[0]["id"]).UpdateFrom(item[0]) : new Passage().UpdateFrom(item[0], lastSectionId));
                    }
                    else if (item[0]["deleted"] != null && ((bool?)item[0]["deleted"]??false))
                    {
                        delpassages.Add(MyRepository.GetPassage((int?)item[0]["id"]??0).UpdateFrom(item[0]));
                    }
                }
                if (updpassages.Count > 0)
                {
                    //Logger.LogInformation($"updpassages {updpassages.Count} {updpassages}");
                    MyRepository.BulkUpdatePassages(updpassages);
                    int ix = 0;
                    foreach (JArray item in updpass)
                    {
                        item[item.Count - 1]["id"] = updpassages[ix].Id;
                        MyRepository.UpdateSectionModified(updpassages[ix].SectionId);
                        ix++;
                    }
                }
                if (delpassages.Count > 0)
                {
                    MyRepository.BulkDeletePassages(delpassages);
                    delpassages.ForEach(p => MyRepository.UpdateSectionModified(p.SectionId));
                }
                IEnumerable<JToken> delsecs = data.Where(d => ((bool?)d[0]?["issection"]??false) && ((bool?)d[0]?["deleted"]??false));
                List<Section> delsections = new ();
                foreach (JArray item in delsecs)
                {
                    delsections.Add(MyRepository.GetSection((int?)item[0]["id"]??0));
                }
                MyRepository.BulkDeleteSections(delsections);
                MyRepository.UpdatePlanModified(entity.PlanId);
                transaction.Commit();
                entity.Data = JsonConvert.SerializeObject(data);
                entity.Complete = true;
                //TODO
                //ResourceContext contextEntity = ResourceGraph.GetResourceContext("sectionpassages");
                //contextEntity.Attributes.Where(a => a.PublicName == "data").First() = entity.Data;
                await UpdateAsync(entity.Id, entity, new CancellationToken());
                return entity;
            }
            catch (Exception ex)
            {
                Logger.LogCritical($"Insert Error {ex}");
                /* I'm giving up...let the next one try */
                transaction.Rollback();
                await MyRepository.DeleteAsync(entity, entity.Id, new CancellationToken());
                throw new JsonApiException(new ErrorObject(System.Net.HttpStatusCode.InternalServerError), new Exception(ex.Message));
            }
        }
     }
}
