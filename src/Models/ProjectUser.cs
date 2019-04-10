using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class ProjectUser : BaseModel
    {
        [HasOne("user")]
        public virtual User User { get; set; }

        [Attr("user-id")]
        public int UserId { get; set; }

        [HasOne("project")]
        public virtual Project Project { get; set; }

        [Attr("project-id")]
        public int ProjectId { get; set; }

        [HasOne("role", Link.None)]
        public virtual Role Role { get; set; }

        [Attr("role-id")]
        public int RoleId { get; set; }

        [Attr("font")]
        public string Font { get; set; }

        [Attr("font-size")]
        public string FontSize { get; set; }

    }
}
