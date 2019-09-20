using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class Passage : BaseModel, IArchive
    {
        [Attr("sequencenum")]
        public int Sequencenum { get; set; }
        [Attr("book")]
        public string Book { get; set; }
        [Attr("reference")]
        public string Reference { get; set; }
        [Attr("position")]
        public double? Position { get; set; }
        [Attr("state")]
        public string State { get; set; }
        [Attr("hold")]
        public Boolean Hold { get; set; }
        [Attr("title")]
        public string Title { get; set; }

        [HasMany("mediafiles")]
        public virtual List<Mediafile> Mediafiles { get; set; }

 //     [NotMapped]
 //     [HasManyThrough(nameof(PassageSections))]
 //     public List<Section> Sections { get; set; }
        [HasMany("passage-sections")]
        public List<PassageSection> PassageSections { get; set; }
        [HasMany("user-passages")]
        public List<UserPassage> UserPassages { get; set; }
        public bool Archived { get; set; }

        public bool ReadyToSync
        {
            get { return State == "approved"; }
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
