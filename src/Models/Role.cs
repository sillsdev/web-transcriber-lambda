using JsonApiDotNetCore.Models;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Role : BaseModel
    {
        public RoleName Rolename { get; set; }

        [Attr("role-name")]
        public string RoleNameString
        {
            get
            {
                return Rolename.ToString();
            }
        }

        [HasMany("user-roles", Link.None)]
        public virtual List<UserRole> UserRoles { get; set; }
    }
}
