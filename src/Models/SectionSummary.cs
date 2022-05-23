namespace SIL.Transcriber.Models
{
    public class SectionSummary
    {
        public SectionSummary():base() { section = new(); }
        public Section section;
        public string? Book { get; set; }
        public int StartChapter { get; set; }
        public int EndChapter { get; set; } 
        public int StartVerse { get; set; }
        public int EndVerse { get; set; }
        public string SectionHeader(bool addNumbers = true) { return (addNumbers ? section.Sequencenum.ToString() + " - " : "") + section.Name; }
    }
}
