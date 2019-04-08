using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Products
    {
        public Products()
        {
            Productartifacts = new HashSet<Productartifacts>();
            Productbuilds = new HashSet<XProductbuilds>();
            Producttransitions = new HashSet<Producttransitions>();
        }

        public Guid Id { get; set; }
        public int Projectid { get; set; }
        public int Productdefinitionid { get; set; }
        public int? Storeid { get; set; }
        public int? Storelanguageid { get; set; }
        public DateTime? Datecreated { get; set; }
        public DateTime? Dateupdated { get; set; }
        public int Workflowjobid { get; set; }
        public int Workflowbuildid { get; set; }
        public DateTime? Datebuilt { get; set; }
        public int Workflowpublishid { get; set; }
        public string Workflowcomment { get; set; }
        public DateTime? Datepublished { get; set; }
        public string Publishlink { get; set; }

        public Productdefinitions Productdefinition { get; set; }
        public Project Project { get; set; }
        public Stores Store { get; set; }
        public Storelanguage Storelanguage { get; set; }
        public ICollection<Productartifacts> Productartifacts { get; set; }
        public ICollection<XProductbuilds> Productbuilds { get; set; }
        public ICollection<Producttransitions> Producttransitions { get; set; }
    }
}
