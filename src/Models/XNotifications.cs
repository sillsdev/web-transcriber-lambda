using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Notifications
    {
        public int Id { get; set; }
        public string Messageid { get; set; }
        public int Userid { get; set; }
        public DateTime? Dateread { get; set; }
        public DateTime? Dateemailsent { get; set; }
        public DateTime? Datecreated { get; set; }
        public DateTime? Dateupdated { get; set; }
        public string Message { get; set; }
        public string Messagesubstitutionsjson { get; set; }
        public bool? Sendemail { get; set; }

        public User User { get; set; }
    }
}
