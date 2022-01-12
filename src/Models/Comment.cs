
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class Comment : BaseModel, IArchive
    {
        public int? DiscussionId { get; set; }
        [HasOne("discussion", Link.None)]
        public Discussion Discussion { get; set; }
        public int? MediafileId { get; set; }
        [HasOne("mediafile", Link.None)]
        public Mediafile Mediafile { get; set; }
        [Attr("comment-text")]
        public string CommentText { get; set; }
        [Attr("offline-id")]
        public string OfflineId { get; set; }
        [Attr("offline-discussion-id")]
        public string OfflineDiscussionId { get; set; }
        [Attr("offline-mediafile-id")]
        public string OfflineMediafileId { get; set; }
        public bool Archived { get; set; }
    }
}
