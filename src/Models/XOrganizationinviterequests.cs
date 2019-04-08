using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Organizationinviterequests
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Orgadminemail { get; set; }
        public string Websiteurl { get; set; }
        public DateTime? Datecreated { get; set; }
        public DateTime? Dateupdated { get; set; }
    }
}
