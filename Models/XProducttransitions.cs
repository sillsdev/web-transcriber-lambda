using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Producttransitions
    {
        public int Id { get; set; }
        public Guid Productid { get; set; }
        public Guid? Workflowuserid { get; set; }
        public string Allowedusernames { get; set; }
        public string Initialstate { get; set; }
        public string Destinationstate { get; set; }
        public string Command { get; set; }
        public DateTime? Datetransition { get; set; }

        public Products Product { get; set; }
    }
}
