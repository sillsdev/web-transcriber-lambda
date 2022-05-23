using System.Xml.Linq;

namespace SIL.Paratext.Models
{
    public class ParatextChapter
    {
        public ParatextChapter():base()
        {
            Project = "";
            Book = "";
        }
        public string Project { get; set; }
        public string Book { get; set; }
        public int Chapter { get; set; }
        public string? Revision { get; set; }
        public string? OriginalValue { get; set; }
        public XElement? OriginalUSX { get; set; }
        public string? NewValue { get; set; }
        public XElement? NewUSX { get; set; }
    }
}
