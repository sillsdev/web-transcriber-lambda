using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class ProjectIntegration :BaseModel
    {
        [Attr("project-id")]
        public int ProjectId { get; set; }
        [Attr("integration-id")]
        public int IntegrationId { get; set; }

        [HasOne("integration")]
        public virtual Integration Integration { get; set; }
        [HasOne("project")]
        public virtual Project Project { get; set; }
    }
}
