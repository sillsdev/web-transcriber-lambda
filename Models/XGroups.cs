using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Groups
    {
        public Groups()
        {
            Groupmemberships = new HashSet<Groupmemberships>();
            Projects = new HashSet<Project>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Abbreviation { get; set; }
        public int Ownerid { get; set; }

        public Organization Owner { get; set; }
        public ICollection<Groupmemberships> Groupmemberships { get; set; }
        public ICollection<Project> Projects { get; set; }
    }
}
