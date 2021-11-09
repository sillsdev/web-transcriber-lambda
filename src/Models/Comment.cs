
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class Comment : BaseModel, IArchive
    {
        [Attr("subject")]
        public int Discussionid { get; set; }
        [HasOne("discussion")]
        public Discussion Discussion { get; set; }
        public int Mediafileid { get; set; }
        [HasOne("mediafile")]
        public Mediafile Mediafile { get; set; }
        public string CommentText { get; set; }
        public bool Archived { get; set; }
    }
}
