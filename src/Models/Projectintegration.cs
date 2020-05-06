using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public partial class ProjectIntegration :BaseModel, IArchive
    {
        [Attr("project-id")]
        public int ProjectId { get; set; }
        [Attr("integration-id")]
        public int IntegrationId { get; set; }

        [HasOne("integration", Link.None)]
        public virtual Integration Integration { get; set; }
        [HasOne("project", Link.None)]
        public virtual Project Project { get; set; }

        [Attr("settings")]
        [Column(TypeName = "jsonb")]
        public string Settings { get; set; }

        public bool Archived { get; set; }

    }
}
