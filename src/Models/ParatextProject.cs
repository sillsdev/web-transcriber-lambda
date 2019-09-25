using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Paratext.Models
{
    public class ParatextProject
    {
        public string ParatextId { get; set; }
        public string Name { get; set; }
        public string LanguageTag { get; set; }
        public string LanguageName { get; set; }
        public string ProjectId { get; set; }
        public bool IsConnectable { get; set; }
        public bool IsConnected { get; set; }
    }
}
