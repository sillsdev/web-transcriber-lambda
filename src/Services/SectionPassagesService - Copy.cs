using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SIL.Transcriber.Services
{
    public class SectionPassagesService
    {
        protected readonly AppDbContext dbContext;
        protected IJsonApiContext JsonApiContext { get; }

        public SectionPassagesService(IDbContextResolver contextResolver, IJsonApiContext jsonApiContext)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            JsonApiContext = jsonApiContext;
        }
        public class IRecord
        {
            public bool issection;
            public string id;
            public bool changed;
            public int sequencenum;
            public string book;
            public string reference;
            public string title;
            public int planid;

            public IRecord(JToken v)
            {
                issection = (bool)v["issection"];
                id = (string)v["id"];
                changed = (bool)v["changed"];
                if (changed)
                {
                    int seq = 0;
                    sequencenum = int.TryParse((string)v["sequencenum"], out seq) ? seq : 0;
                    book = (string)v["book"];
                    reference = (string)v["reference"];
                    title = (string)v["title"];
                    planid = (int)v["planid"];
                }
            }

            public static explicit operator IRecord(JToken v) => new IRecord(v);
        }
        public SectionPassage PostAsync(SectionPassage entity)
        {
            var input =  JsonConvert.DeserializeObject(entity.Data);

            if (!input.GetType().IsAssignableFrom(typeof(JArray))) throw new Exception("Invalid input");

            JArray data = (JArray)input;

            if (data.Count==0) return entity;

            int lastSection = 0;
            Section section;
            Passage passage;
            SectionPassage inprogress = dbContext.Sectionpassages.Where(e => e.uuid == entity.uuid).FirstOrDefault();
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
                    throw new Exception("orbit is dumb");
                }
            }
            dbContext.Add(entity);
            dbContext.SaveChanges();
            using (var transaction = dbContext.Database.BeginTransaction())
            {

                try
                {
                    var newsecs = data.Where(d => (string)d["id"] == "" && (bool)d["issection"]);
                    //add all sections
                    var newsections = new List<Section>();
                    foreach (var item in newsecs)
                    {
                        int seq = 0;
                        section = new Section
                        {
                            Name = (string)item["title"],
                            Sequencenum = int.TryParse((string)item["sequencenum"], out seq) ? seq : 0,
                            PlanId = (int)item["planid"]
                        };
                        newsections.Add(section);
                    }
                    if (newsections.Count > 0)
                    {
                        dbContext.BulkInsert(newsections);
                    }

                    foreach (var item in data)
                    {

                        IRecord rec = (IRecord)item;
                        int id = 0;
                        if (rec.changed)
                        {
                            Debug.WriteLine("here " + rec.id + " " + rec.title + " " + rec.reference);
                            if (int.TryParse(rec.id, out id))
                            { //update the record 
                                if (rec.issection)
                                {
                                    section = dbContext.Sections.Find(id);
                                    section.Name = rec.title;
                                    section.Sequencenum = rec.sequencenum;
                                    dbContext.Sections.Update(section);
                                    dbContext.SaveChanges();
                                }
                                else
                                {
                                    passage = dbContext.Passages.Find(id);
                                    passage.Book = rec.book;
                                    passage.Reference = rec.reference;
                                    passage.Title = rec.title;
                                    passage.Sequencenum = rec.sequencenum;
                                    dbContext.Passages.Update(passage);
                                    dbContext.SaveChanges();
                                }
                            }
                            else
                            { //insert the record 
                                if (rec.issection)
                                {
                                    section = new Section
                                    {
                                        Name = rec.title,
                                        Sequencenum = rec.sequencenum,
                                        PlanId = rec.planid,
                                    };
                                    newsections.Add(section);
                                    /*
                                    dbContext.Sections.Add(section);
                                    dbContext.SaveChanges();
                                    rec.id = section.Id.ToString();
                                    item["id"] = rec.id;
                                    lastSection = section.Id;
                                    Debug.WriteLine("new section " + rec.id + " " + item["id"]);
                                    */
                                }
                                else
                                {
                                    passage = new Passage
                                    {
                                        Book = rec.book,
                                        Reference = rec.reference,
                                        Title = rec.title,
                                        Sequencenum = rec.sequencenum,
                                        State = "noMedia",
                                        SectionId = lastSection,
                                    };
                                    dbContext.Passages.Add(passage);
                                    dbContext.SaveChanges();
                                    rec.id = passage.Id.ToString();
                                    item["id"] = rec.id;
                                    Debug.WriteLine("new passage " + rec.id + " " + item["id"]);

                                }
                            }

                        }
                        else if (rec.issection)
                            int.TryParse(rec.id, out lastSection);
                    }
                    transaction.Commit();
                    entity.Data = JsonConvert.SerializeObject(data);
                    entity.Complete = true;
                    dbContext.Update(entity);
                    dbContext.SaveChanges();
                    var contextEntity = JsonApiContext.ResourceGraph.GetContextEntity("sectionpassages");
                    JsonApiContext.AttributesToUpdate[contextEntity.Attributes.Where(a => a.PublicAttributeName == "data").First()] = entity.Data;

                }
                catch (Exception ex)
                {
                    /* I'm giving up...let the next one try */
                    transaction.Rollback();
                    dbContext.Remove(entity);
                    dbContext.SaveChanges();
                    throw ex;
                }
            }
            return entity;
        }
    }
}
