
using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class Comment : BaseModel, IArchive
    {
        [Attr(PublicName = "discussion-id")]
        public int? DiscussionId { get; set; }
        [HasOne(PublicName = "discussion")]
        public Discussion? Discussion { get; set; }
        [Attr(PublicName = "mediafile-id")] 
        public int? MediafileId { get; set; }
        [HasOne(PublicName = "mediafile")]
        public Mediafile? Mediafile { get; set; }
        [Attr(PublicName = "comment-text")]
        public string? CommentText { get; set; }
        [Attr(PublicName = "offline-id")]
        public string? OfflineId { get; set; }
        [Attr(PublicName = "offline-discussion-id")]
        public string? OfflineDiscussionId { get; set; }
        [Attr(PublicName = "offline-mediafile-id")]
        public string? OfflineMediafileId { get; set; }
        [Attr(PublicName = "visible")]
        [Column(TypeName = "jsonb")]
        public string? Visible { get; set; }
        public bool Archived { get; set; }
    }
}
