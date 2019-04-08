using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Organizationinvites
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Owneremail { get; set; }
        public string Token { get; set; }
    }
}
