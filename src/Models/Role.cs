using JsonApiDotNetCore.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public partial class Role : BaseModel
    {
        [Attr("org-role")]
        public bool Orgrole { get; set; }
        [Attr("group-role")]
        public bool Grouprole { get; set; }

        public RoleName Rolename { get; set; }

        [Attr("role-name")]
        public string RoleNameString
        {
            get
            {
                return Rolename.ToString();
            }
        }
    }
}
