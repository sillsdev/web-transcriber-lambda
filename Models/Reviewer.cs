using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Reviewer : BaseModel
    {
        [Attr("name")]
        public string Name { get; set; }
        [Attr("email")]
        public string Email { get; set; }

        [Attr("project-id")]
        public int Projectid { get; set; }

        [HasOne("project")]
        public virtual Project Project { get; set; }
    }
}
