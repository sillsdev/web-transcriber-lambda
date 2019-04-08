using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Workflowprocessscheme
    {
        public Guid Id { get; set; }
        public string Scheme { get; set; }
        public string Definingparameters { get; set; }
        public string Definingparametershash { get; set; }
        public string Schemecode { get; set; }
        public bool Isobsolete { get; set; }
        public string Rootschemecode { get; set; }
        public Guid? Rootschemeid { get; set; }
        public string Allowedactivities { get; set; }
        public string Startingtransition { get; set; }
    }
}
