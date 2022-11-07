using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{

    public class Projdata : BaseModel
    {

        public Projdata()
        {
            Id = 1;
            Json = "{}";
        }
        [Attr(PublicName = "start-index")]
        public int StartIndex { get; set; }
        [Attr(PublicName = "startnext")]
        public int StartNext { get; set; }
        [Attr(PublicName = "project-id")]
        public int ProjectId { get; set; }
        [Attr(PublicName = "snapshotdate")]
        public string? SnapshotDate { get; set; }
        [Attr(PublicName = "json")]
        public string Json { get; set; }
    }
}
