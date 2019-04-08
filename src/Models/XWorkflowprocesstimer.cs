using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Workflowprocesstimer
    {
        public Guid Id { get; set; }
        public Guid Processid { get; set; }
        public string Name { get; set; }
        public DateTime Nextexecutiondatetime { get; set; }
        public bool Ignore { get; set; }
    }
}
