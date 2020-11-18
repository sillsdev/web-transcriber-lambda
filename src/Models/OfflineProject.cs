using JsonApiDotNetCore.Models;
using System;

namespace SIL.Transcriber.Models
{
    public partial class OfflineProject : BaseModel
    {
        [Attr("computerfp")]
        public string Computerfp { get; set; }

        public int ProjectId { get; set; }
        [HasOne("project", Link.None)]
        public virtual Project Project { get; set; }

        [Attr("snapshot-date")]
        public DateTime SnapshotDate { get; set; }

    }
}
