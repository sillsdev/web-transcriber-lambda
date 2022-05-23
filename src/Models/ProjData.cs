using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{

    public class ProjData : BaseModel
    {

           public ProjData()
            {
                Id = 1;
                Json = "{}";
            }
            [Attr(PublicName="json")]
            public string Json { get; set; }
            [Attr(PublicName = "start-index")]
            public int StartIndex { get; set; }
            [Attr(PublicName="project-id")]
            public int ProjectId { get; set; }
            [Attr(PublicName="snapshotdate")]
            public string? SnapshotDate { get; set; }
    }
}
