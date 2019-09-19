using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SIL.Transcriber.Models
{
    public class ParatextChapter
    {
        public string Project { get; set; }
        public string Book { get; set; }
        public int Chapter { get; set; }
        public string Revision { get; set; }
        public string OriginalValue { get; set; }
        public string OriginalUSX { get; set; }
        public string NewValue { get; set; }
        public string NewUSX { get; set; }
    }
}
