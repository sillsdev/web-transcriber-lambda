using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class ProjectSet : BaseModel
    {
        [HasOne("set")]
        public virtual Set Set { get; set; }

        [Attr("set-id")]
        public int SetId { get; set; }

        [HasOne("project")]
        public virtual Project Project { get; set; }

        [Attr("project-id")]
        public int ProjectId { get; set; }

    }
}
