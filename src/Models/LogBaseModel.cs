
using JsonApiDotNetCore.Models;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SIL.Transcriber.Models;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Logging.Models
{
    public class LogBaseModel : BaseModel
    {
        public LogBaseModel() : base()
        {
        }
        public LogBaseModel(int userid) : base()
        {
            UserId = userid;
        }
        [NotMapped]
        [Attr("userid")]
        public int UserId { get; set; }

    }
}