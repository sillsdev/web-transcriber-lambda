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
                    throw new JsonApiException(new Error(502,"orbit is dumb"));
                }
            }
            await MyRepository.CreateAsync(entity);
            using (Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = MyRepository.BeginTransaction())
            {
                try
                {
                    IEnumerable<JToken> newsecs = data.Where(d => d["id"] == null && (bool)d["issection"]);
                    IEnumerable<JToken> updsecs = data.Where(d => d["id"] != null && (bool)d["issection"] && (bool)d["changed"]);
                    //add all sections
                    List<Section> newsections = new List<Section>();
                    List<Section> updsections = new List<Section>();
                    foreach (JToken item in newsecs)
                    {
                        newsections.Add(new Section(item));
                    }
                    foreach (JToken item in updsecs)
                    {
                        updsections.Add(MyRepository.GetSection((int)item["id"]).UpdateFrom(item));
                    }
                    if (newsections.Count > 0)
                    {
                        MyRepository.BulkInsertSections(newsections);
                        int ix = 0;
                        foreach (JToken item in newsecs)
                        {
                            item["id"] = newsections[ix].Id;
                            ix++;
                        }
                    }
                    if (updsections.Count > 0)
                    {
                        MyRepository.BulkUpdateSections(updsections);
                    }
                    int lastSectionId=0;
                    /* process all the passages now */
                    List<Passage> newpassages = new List<Passage>();
                    List<Passage> updpassages = new List<Passage>();
                    foreach (JToken item in data)
                    {
                        if ((bool)item["issection"])
                        {
                            lastSectionId = (int)item["id"];
                        }
                        else if ((bool)item["changed"])
                        {
                            if (item["id"] == null)
                                newpassages.Add(new Passage(item, lastSectionId));
                            else
                                updpassages.Add(MyRepository.GetPassage((int)item["id"]).UpdateFrom(item));
                        }
                    }
                    if (newpassages.Count > 0)
                    {
                        IEnumerable<JToken> newpsgs = data.Where(d => d["id"] == null && !(bool)d["issection"]);
                       
                        MyRepository.BulkInsertPassages(newpassages);
                        int ix = 0;
                        foreach (JToken item in newpsgs)
                        {
                            item["id"] = newpassages[ix].Id;
                            ix++;
                        }
                    }
                    if (updpassages.Count > 0)
                    {
                        MyRepository.BulkUpdatePassages(updpassages);
                    }
                    Logger.LogInformation($"Success! committing...{data}");

                    transaction.Commit();
                    entity.Data = JsonConvert.SerializeObject(data);
                    entity.Complete = true;
                    await MyRepository.UpdateAsync(entity.Id, entity);
                    ContextEntity contextEntity = JsonApiContext.ResourceGraph.GetContextEntity("sectionpassages");
                    JsonApiContext.AttributesToUpdate[contextEntity.Attributes.Where(a => a.PublicAttributeName == "data").First()] = entity.Data;
                    return entity;
                }
                catch (Exception ex)
                {
                    Logger.LogCritical($"Insert Error {ex}");
                    /* I'm giving up...let the next one try */
                    transaction.Rollback();
                    await MyRepository.DeleteAsync(entity.Id);
                    throw new JsonApiException(new Error(502,ex.Message));
                }
            }
        }
     }
}
