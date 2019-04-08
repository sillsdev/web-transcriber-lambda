using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class XSystemstatuses
    {
        public int Id { get; set; }
        public string Buildengineurl { get; set; }
        public string Buildengineapiaccesstoken { get; set; }
        public bool Systemavailable { get; set; }
        public DateTime? Datecreated { get; set; }
        public DateTime? Dateupdated { get; set; }
    }
}
