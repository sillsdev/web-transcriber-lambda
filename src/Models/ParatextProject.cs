namespace SIL.Paratext.Models
{
    public class ParatextProject
    {
        public ParatextProject() : base()
        {
            ParatextId = "";
            Name = "";
            LanguageTag = "";
        }
        public string ParatextId { get; set; }
        public string Name { get; set; }
        public string LanguageTag { get; set; }
        public string? LanguageName { get; set; }
        public IEnumerable<string>? ProjectIds { get; set; }
        public bool IsConnected { get; set; }
        public bool IsConnectable { get; set; }
        public string? CurrentUserRole { get; set; }
        public string? ProjectType { get; internal set; }
        public string? ShortName { get; internal set; }
        public string? BaseProject { get; internal set; }
    }
}
