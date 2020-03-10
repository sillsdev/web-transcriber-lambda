﻿using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class OrgData : Identifiable<int>
    {
        public OrgData(string data)
        {
            Json = data;
            Id = 1;
        }
        [NotMapped]
        [Attr("json")]
        public string Json { get; set; }
    }
}