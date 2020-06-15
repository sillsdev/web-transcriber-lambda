using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal;
using SIL.Transcriber.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace SIL.Transcriber.Services
{
    public class SectionPassageService : EntityResourceService<SectionPassage>
    {
        protected SectionPassageRepository MyRepository { get; }
        protected IJsonApiContext JsonApiContext { get; }
        protected ILogger<SectionPassage> Logger { get; set; }
        public SectionPassageService(IJsonApiContext jsonApiContext, SectionPassageRepository myRepository, ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
            this.MyRepository = myRepository;
            JsonApiContext = jsonApiContext;
            this.Logger = loggerFactory.CreateLogger<SectionPassage>();
        }
        public async Task<SectionPassage> PostAsync(SectionPassage entity)
        {
            object input = JsonConvert.DeserializeObject(entity.Data);
            
            if (input==null || !input.GetType().IsAssignableFrom(typeof(JArray))) throw new Exception("Invalid input");

            JArray data = (JArray)input;

            if (data.Count == 0) return entity;

            SectionPassage inprogress = MyRepository.GetByUUID(entity.uuid) ;
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
                await MyRepository.CreateAsync(entity);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{ex}");
                if (ex.InnerException != null && ex.InnerException.Message.Contains("23505"))
                    return null;
            }
            using (IDbContextTransaction transaction = MyRepository.BeginTransaction())
            {
                try
                {
                    IEnumerable<JToken> updsecs = data.Where(d => (bool)d[0]["issection"] && (bool)d[0]["changed"]);
                    //add all sections
                    List<Section> updsections = new List<Section>();

                    foreach (JArray item in updsecs)
                    {
                        updsections.Add(item[0]["id"] != null ? MyRepository.GetSection((int)item[0]["id"]).UpdateFrom(item[0]) : new Section(item[0], entity.PlanId));
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
                    foreach (JArray item in data)
                    {
                        if ((bool)item[0]["issection"])
                        {
                            lastSectionId = (int)item[0]["id"];
                            if (item.Count > 1 && (bool)item[1]["changed"])
                            {
                                updpass.Add(item);
                                updpassages.Add(item[1]["id"] != null ? MyRepository.GetPassage((int)item[1]["id"]).UpdateFrom(item[1]) : new Passage(item[1], lastSectionId));
                            }
                        }
                        else if ((bool)item[0]["changed"])
                        {
                            updpass.Add(item);
                            updpassages.Add(item[0]["id"] != null ? MyRepository.GetPassage((int)item[0]["id"]).UpdateFrom(item[0]): new Passage(item[0], lastSectionId));
                        }
                    }
                     if (updpassages.Count > 0)
                    {
                        //Logger.LogInformation($"updpassages {updpassages.Count} {updpassages}");
                        MyRepository.BulkUpdatePassages(updpassages);
                        int ix = 0;
                        foreach (JArray item in updpass)
                        {
                            item[item.Count-1]["id"] = updpassages[ix].Id;
                            ix++;
                        }
                    }
                    transaction.Commit();
                    entity.Data = JsonConvert.SerializeObject(data);
                    entity.Complete = true;
                    ContextEntity contextEntity = JsonApiContext.ResourceGraph.GetContextEntity("sectionpassages");
                    JsonApiContext.AttributesToUpdate[contextEntity.Attributes.Where(a => a.PublicAttributeName == "data").First()] = entity.Data;
                    await MyRepository.UpdateAsync(entity.Id, entity);
                    return entity;
                }
                catch (Exception ex)
                {
                    Logger.LogCritical($"Insert Error {ex}");
                    /* I'm giving up...let the next one try */
                    transaction.Rollback();
                    await MyRepository.DeleteAsync(entity.Id);
                    throw new JsonApiException(new Error(502, ex.Message));
                }
            }
        }
     }
}
