using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Workflowprocesstransitionhistory
    {
        public Guid Id { get; set; }
        public Guid Processid { get; set; }
        public string Executoridentityid { get; set; }
        public string Actoridentityid { get; set; }
        public string Fromactivityname { get; set; }
        public string Toactivityname { get; set; }
        public string Tostatename { get; set; }
        public DateTime Transitiontime { get; set; }
        public string Transitionclassifier { get; set; }
        public string Fromstatename { get; set; }
        public string Triggername { get; set; }
        public bool Isfinalised { get; set; }
    }
}
