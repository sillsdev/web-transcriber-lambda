﻿using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table("roles")]
    public partial class Role : BaseModel
    {
        [Attr(PublicName = "org-role")]
        public bool Orgrole { get; set; }
        [Attr(PublicName = "group-role")]
        public bool Grouprole { get; set; }

        public RoleName Rolename { get; set; }

        [Attr(PublicName = "role-name")]
        public string RoleNameString {
            get {
                return Rolename.ToString();
            }
        }
    }
}
