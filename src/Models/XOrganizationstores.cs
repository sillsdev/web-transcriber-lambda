using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Organizationstores
    {
        public int Id { get; set; }
        public int Organizationid { get; set; }
        public int Storeid { get; set; }

        public Organization Organization { get; set; }
        public Stores Store { get; set; }
    }
}
