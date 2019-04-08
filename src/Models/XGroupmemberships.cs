using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Groupmemberships
    {
        public int Id { get; set; }
        public int Userid { get; set; }
        public int Groupid { get; set; }

        public Groups Group { get; set; }
        public User User { get; set; }
    }
}
