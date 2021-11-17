
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class Comment : BaseModel, IArchive
    {
        public int DiscussionId { get; set; }
        [HasOne("discussion", Link.None)]
        public Discussion Discussion { get; set; }
        public int? MediafileId { get; set; }
        [HasOne("mediafile", Link.None)]
        public Mediafile Mediafile { get; set; }
        [Attr("comment-text")]
        public string CommentText { get; set; }
        public bool Archived { get; set; }
    }
}
