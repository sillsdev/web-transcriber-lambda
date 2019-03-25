using JsonApiDotNetCore.Models;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Role : BaseModel
    {
        [Attr("rolename")]
        public string Rolename { get; set; }

        [HasMany("user-roles", Link.None)]
        public virtual List<UserRole> UserRoles { get; set; }
    }
}
