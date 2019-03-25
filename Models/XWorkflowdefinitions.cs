using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Workflowdefinitions
    {
        public Workflowdefinitions()
        {
            Productdefinitions = new HashSet<Productdefinitions>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Description { get; set; }
        public string Workflowscheme { get; set; }
        public string Workflowbusinessflow { get; set; }
        public int? Storetypeid { get; set; }

        public Storetypes Storetype { get; set; }
        public ICollection<Productdefinitions> Productdefinitions { get; set; }
    }
}
