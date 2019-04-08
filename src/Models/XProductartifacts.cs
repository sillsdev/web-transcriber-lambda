using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Productartifacts
    {
        public int Id { get; set; }
        public Guid Productid { get; set; }
        public int Productbuildid { get; set; }
        public string Artifacttype { get; set; }
        public string Url { get; set; }
        public long? Filesize { get; set; }
        public string Contenttype { get; set; }
        public DateTime? Datecreated { get; set; }
        public DateTime? Dateupdated { get; set; }

        public Products Product { get; set; }
        public XProductbuilds Productbuild { get; set; }
    }
}
