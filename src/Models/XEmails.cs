using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class XEmails
    {
        public int Id { get; set; }
        public string To { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public string Subject { get; set; }
        public string Contenttemplate { get; set; }
        public string Contentmodeljson { get; set; }
        public DateTime Created { get; set; }
    }
}
