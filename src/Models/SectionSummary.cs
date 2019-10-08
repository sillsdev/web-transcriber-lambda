namespace SIL.Transcriber.Models
{
    public class SectionSummary
    {
        public Section section;
        public string Book { get; set; }
        public int startChapter { get; set; }
        public int endChapter { get; set; } 
        public int startVerse { get; set; }
        public int endVerse { get; set; }
        public string SectionHeader() { return section.Sequencenum.ToString() + " - " + section.Name; }
    }
}
