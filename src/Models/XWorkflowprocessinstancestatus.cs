using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Workflowprocessinstancestatus
    {
        public Guid Id { get; set; }
        public short Status { get; set; }
        public Guid Lock { get; set; }
    }
}
