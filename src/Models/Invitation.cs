using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;
using System.Linq;

namespace SIL.Transcriber.Models
{
    public partial class Invitation : BaseModel, IBelongsToOrganization
    {
        [Attr("email")]
        public string Email { get; set; }
        [Attr("accepted")]
        public bool Accepted { get; set; }
        [Attr("login-link")]
        public string LoginLink { get; set; }
        [Attr("invited-by")]
        public string InvitedBy { get; set; }
        [Attr("sil-id")]
        public int SilId { get; set; }

        [NotMapped]
        [Attr("strings")]
        public string Strings { get; set; }


        [HasOne("organization", Link.None)]
        public virtual Organization Organization { get; set; }
        public int OrganizationId { get; set; }

        [HasOne("role", Link.None)]
        public virtual Role Role { get; set; }
        public int RoleId { get; set; }

        [HasOne("group", Link.None)]
        public virtual Group Group { get; set; }
        public int? GroupId { get; set; }

        [HasOne("group-role", Link.None)]
        public virtual Role GroupRole { get; set; }
        public int? GroupRoleId { get; set; }

        [HasOne("all-users-role", Link.None)]
        public virtual Role AllUsersRole { get; set; }
        public int AllUsersRoleId { get; set; }

    }
}
