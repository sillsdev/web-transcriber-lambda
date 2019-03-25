using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Workflowglobalparameter
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
