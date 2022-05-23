
using SIL.Transcriber.Models;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;

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
        [Attr(PublicName="userid")]
        public int UserId { get; set; }

    }
}