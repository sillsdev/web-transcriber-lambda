using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System;
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

            foreach (var item in data)
            {
                IRecord rec = (IRecord)item;
                int id = 0;
                if (rec.changed)
                {
                    Debug.WriteLine("here " + rec.id + " " + rec.title + " " + rec.reference);
                    if (int.TryParse(rec.id, out id))
                    { //update the record 
                        if (rec.issection) {
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
                    else { //insert the record 
                        if (rec.issection)
                        {
                            section = new Section
                            {
                                Name = rec.title,
                                Sequencenum = rec.sequencenum,
                                PlanId = rec.planid,
                            };
                            dbContext.Sections.Add(section);
                            dbContext.SaveChanges();
                            rec.id = section.Id.ToString();
                            item["id"] = rec.id;
                            lastSection = section.Id;
                            Debug.WriteLine("new section " + rec.id + " " + item["id"]);
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

                } else if (rec.issection)
                    int.TryParse(rec.id, out lastSection);
            }
            entity.Data = JsonConvert.SerializeObject(data);
            var contextEntity = JsonApiContext.ResourceGraph.GetContextEntity("sectionpassages");
            JsonApiContext.AttributesToUpdate[contextEntity.Attributes.Where(a => a.PublicAttributeName == "data").First()] = entity.Data;
            return entity;
        }
    }
}
