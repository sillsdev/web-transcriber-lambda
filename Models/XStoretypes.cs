using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Storetypes
    {
        public Storetypes()
        {
            Storelanguages = new HashSet<Storelanguage>();
            Stores = new HashSet<Stores>();
            Workflowdefinitions = new HashSet<Workflowdefinitions>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public ICollection<Storelanguage> Storelanguages { get; set; }
        public ICollection<Stores> Stores { get; set; }
        public ICollection<Workflowdefinitions> Workflowdefinitions { get; set; }
    }
}
