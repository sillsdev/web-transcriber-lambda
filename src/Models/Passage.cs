﻿using JsonApiDotNetCore.Resources.Annotations;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.Passages)]
    public class Passage : BaseModel, IArchive
    {
        public Passage() : base()
        {
            Hold = false;
        }

        public Passage UpdateFrom(JToken item)
        {
            Book = item ["book"]?.ToString() ?? "";
            Reference = item ["reference"]?.ToString() ?? "";
            Title = item ["title"]?.ToString() ?? "";
            Sequencenum = int.TryParse(item ["sequencenum"]?.ToString() ?? "", out int tryint)
                ? tryint
                : 0;
            return this;
        }

        public Passage UpdateFrom(JToken item, int sectionId)
        {
            _ = UpdateFrom(item);
            SectionId = sectionId;
            return this;
        }

        [Attr(PublicName = "sequencenum")]
        public int Sequencenum { get; set; }

        [Attr(PublicName = "book")]
        public string? Book { get; set; }

        [Attr(PublicName = "reference")]
        public string? Reference { get; set; }

        [Attr(PublicName = "state")]
        public string? State { get; set; }

        [Attr(PublicName = "hold")]
        public bool Hold { get; set; }

        [Attr(PublicName = "title")]
        public string? Title { get; set; }

        [Attr(PublicName = "last-comment")]
        public string? LastComment { get; set; }

        [Attr(PublicName = "section-id")]
        public int SectionId { get; set; }

        [HasOne(PublicName = "section")]
        public virtual Section? Section { get; set; }

        public int? OrgWorkflowStepId { get; set; }

        [HasOne(PublicName = "org-workflow-step")]
        public Orgworkflowstep? OrgWorkflowStep { get; set; }

        [Attr(PublicName = "step-complete")]
        [Column(TypeName = "jsonb")]
        public string? StepComplete { get; set; } //json


        [HasOne(PublicName = "shared-resource")]
        public Sharedresource? SharedResource { get; set; }

        [Attr(PublicName = "start-chapter")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int? StartChapter { get; set; }

        [Attr(PublicName = "start-verse")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int? StartVerse { get; set; }

        [Attr(PublicName = "end-chapter")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int? EndChapter { get; set; }

        [Attr(PublicName = "end-verse")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)] 
        public int? EndVerse { get; set; }

        [Attr(PublicName = "passagetype-id")] 
        public int? PassagetypeId { get; set; }

        [HasOne(PublicName = "Passagetype")]
        public virtual Passagetype? Passagetype { get; set; }

        [Attr(PublicName = "plan-id")]
        [NotMapped]
        public int PlanId {
            get { return Section?.PlanId ?? 0; }
        }
        public bool Archived { get; set; }

        public bool ReadyToSync //backward compatibility
        {
            get { return State == "approved" && !Archived; }
        }

        private int startChapter = 0,
            endChapter = 0,
            startVerse = 0,
            endVerse = 0;
        private bool? validScripture = null;
        public bool ValidScripture {
            get {
                validScripture ??= ParseReference(
                        Reference??"",
                        out startChapter,
                        out endChapter,
                        out startVerse,
                        out endVerse
                    );
                return (bool)validScripture;
            }
        }
        public int StartChapter {
            get {
                validScripture ??= ParseReference(
                        Reference??"",
                        out startChapter,
                        out endChapter,
                        out startVerse,
                        out endVerse
                    );
                return startChapter;
            }
        }
        public int EndChapter {
            get {
                validScripture ??= ParseReference(
                        Reference ?? "",
                        out startChapter,
                        out endChapter,
                        out startVerse,
                        out endVerse
                    );
                return endChapter;
            }
        }
        public int StartVerse {
            get {
                validScripture ??= ParseReference(
                        Reference ?? "",
                        out startChapter,
                        out endChapter,
                        out startVerse,
                        out endVerse
                    );
                return startVerse;
            }
        }
        public int EndVerse {
            get {
                validScripture ??= ParseReference(
                        Reference ?? "",
                        out startChapter,
                        out endChapter,
                        out startVerse,
                        out endVerse
                    );
                return endVerse;
            }
        }
        public string Verses {
            get {
                return StartChapter != EndChapter
                    ? Reference ?? ""
                    : StartVerse != EndVerse ? StartVerse.ToString() + "-" + EndVerse.ToString() : StartVerse.ToString();
            }
        }

        private static bool ParseReferencePart(string reference, out int chapter, out int verse)
        {
            int colon = reference.IndexOf(':');
            if (colon >= 0)
            {
                _ = int.TryParse(reference[..colon], out chapter);
                reference = reference [(colon + 1)..];
            }
            else
            {
                chapter = 0;
            }
            return int.TryParse(reference, out verse);
        }

        private static bool ParseReference(
            string reference,
            out int startChapter,
            out int endChapter,
            out int startVerse,
            out int endVerse
        )
        {
            bool ok = reference.Length > 0;
            if (ok)
            {
                int dash = reference.IndexOf("-");
                string firstsection = dash > 0 ? reference[..dash] : reference;
                ok = ParseReferencePart(firstsection, out startChapter, out startVerse);

                endChapter = startChapter;
                endVerse = startVerse;
                if (ok && dash > 0)
                {
                    ok = ParseReferencePart(reference [(dash + 1)..], out endChapter, out endVerse);
                    if (endChapter == 0)
                        endChapter = startChapter;
                }
            } else
            {
                startChapter = 0;
                endChapter = 0;
                startVerse = 0;
                endVerse = 0;
            }
            return ok;
        }
    }
}
