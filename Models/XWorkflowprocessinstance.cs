using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class XWorkflowprocessinstance
    {
        public Guid Id { get; set; }
        public string Statename { get; set; }
        public string Activityname { get; set; }
        public Guid Schemeid { get; set; }
        public string Previousstate { get; set; }
        public string Previousstatefordirect { get; set; }
        public string Previousstateforreverse { get; set; }
        public string Previousactivity { get; set; }
        public string Previousactivityfordirect { get; set; }
        public string Previousactivityforreverse { get; set; }
        public bool Isdeterminingparameterschanged { get; set; }
        public Guid? Parentprocessid { get; set; }
        public Guid Rootprocessid { get; set; }
    }
}
