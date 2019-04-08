using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class XProductbuilds
    {
        public XProductbuilds()
        {
            Productartifacts = new HashSet<Productartifacts>();
        }

        public int Id { get; set; }
        public Guid Productid { get; set; }
        public int Buildid { get; set; }
        public string Version { get; set; }
        public DateTime? Datecreated { get; set; }
        public DateTime? Dateupdated { get; set; }

        public Products Product { get; set; }
        public ICollection<Productartifacts> Productartifacts { get; set; }
    }
}
