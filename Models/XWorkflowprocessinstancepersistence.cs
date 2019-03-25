using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Workflowprocessinstancepersistence
    {
        public Guid Id { get; set; }
        public Guid Processid { get; set; }
        public string Parametername { get; set; }
        public string Value { get; set; }
    }
}
