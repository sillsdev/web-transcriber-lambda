using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;
using Newtonsoft.Json.Linq;

namespace SIL.Transcriber.Models
{
    public class Passage : BaseModel, IArchive
    {
        public Passage() : base()
        {  
        }
        public Passage(JToken item, int sectionId) : base()
        {
            UpdateFrom(item);
            SectionId = sectionId;
        }
        public Passage UpdateFrom(JToken item)
        {
            Book = item["book"] != null ? (string)item["book"] : "";
            Reference = item["reference"] != null ? (string)item["reference"] : "";
            Title = item["title"] != null ? (string)item["title"] : "";
            Sequencenum = int.TryParse((string)item["sequencenum"], out int tryint) ? tryint : 0;
            return this;
        }
        [Attr("sequencenum")]
        public int Sequencenum { get; set; }
        [Attr("book")]
        public string Book { get; set; }
        [Attr("reference")]
        public string Reference { get; set; }
        [Attr("state")]
        public string State { get; set; }
        [Attr("hold")]
        public bool Hold { get; set; }
        [Attr("title")]
        public string Title { get; set; }
        [Attr("last-comment")]
        public string LastComment { get; set; }

        [Attr("section-id")]
        public int SectionId { get; set; }

        [HasOne("section", Link.None)]
        public virtual Section Section { get; set; }

        public int? OrgWorkflowStepId { get; set; }

        [HasOne("org-workflow-step", Link.None)]
        public virtual OrgWorkflowStep OrgWorkflowStep { get; set; }

        [Attr("step-complete")]
        [Column(TypeName = "jsonb")]
        public string StepComplete { get; set; } //json

        [HasMany("mediafiles", Link.None)]
        public virtual List<Mediafile> Mediafiles { get; set; }

        public bool Archived { get; set; }

        public bool ReadyToSync //backward compatibility
        {
            get { return State == "approved" && !Archived; }
        }

        private int startChapter = 0, endChapter = 0, startVerse = 0, endVerse = 0;
        public int StartChapter
        {
            get
            {
                if (startChapter > 0 || Reference == null)
                    return startChapter;
                ParseReference(Reference, out startChapter, out endChapter, out startVerse, out endVerse);
                return startChapter;
            }
        }
        public int EndChapter
        {
            get
            {
                if (endChapter > 0 || Reference == null)
                    return endChapter;
                ParseReference(Reference, out startChapter, out endChapter, out startVerse, out endVerse);
                return endChapter;

            }
        }
        public int StartVerse
        {
            get
            {
                if (startVerse > 0 || Reference == null)
                    return startVerse;
                ParseReference(Reference, out startChapter, out endChapter, out startVerse, out endVerse);
                return startVerse;

            }
        }
        public int EndVerse
        {
            get
            {
                if (endVerse > 0 || Reference == null)
                    return endVerse;
                ParseReference(Reference, out startChapter, out endChapter, out startVerse, out endVerse);
                return endVerse;
            }
        }
        public string Verses
        {
            get
            {
                if (StartChapter != EndChapter)
                    return Reference;
                if (StartVerse != EndVerse)
                    return StartVerse.ToString() + "-" + EndVerse.ToString();
                return StartVerse.ToString();
            }
        }

        private bool ParseReferencePart(string reference, out int chapter, out int verse)
        {
            bool ok;
            var colon = reference.IndexOf(':');
            if (colon >= 0)
            {
                ok = int.TryParse(reference.Substring(0, colon), out chapter);
                reference = reference.Substring(colon + 1);
            }
            else
            {
                chapter = 0;
            }
            ok = int.TryParse(reference, out verse);
            return ok;
        }
        private bool ParseReference(string reference, out int startChapter, out int endChapter, out int startVerse, out int endVerse)
        {
            bool ok;
            var dash = reference.IndexOf("-");
            var firstsection = dash > 0 ? reference.Substring(0, dash) : reference;
            ok = ParseReferencePart(firstsection, out startChapter, out startVerse);

            if (startChapter == 0)
                startChapter = 1;

            endChapter = startChapter;
            endVerse = startVerse;
            if (ok && dash > 0)
            {
                ok = ParseReferencePart(reference.Substring(dash + 1), out endChapter, out endVerse);
                if (endChapter == 0)
                    endChapter = startChapter;
            }
            return ok;
        }
    }
}
