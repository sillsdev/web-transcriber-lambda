

using System.Collections.Generic;

namespace SIL.Paratext.Models
{
    public class ParatextOrg
    {
        public ParatextOrg():base()
        {
            Id = "";
            Name = "";
            NameLocal = "";
        }
        public string Id { get; set; }
        public string Name { get; set; }
        public string NameLocal { get; set; }
        public string? Url { get; set; }
        public string? Abbr { get; set; }
        public string? Parent { get; set; }
        public string? Location { get; set; }
        public string? Area { get; set; }
        public bool Public { get; set; }
        public bool Active { get; set; }
        public bool InDbl { get; set; }
        public bool AuthorizedForParatext { get; set; }
        public bool ShareBasicProgressInfo { get; set; }
        public string? CountryISO { get; set; }
        public List<string>? Domains { get; set; }
    }
}
