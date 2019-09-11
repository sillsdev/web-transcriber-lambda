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
        [Attr("sil-id")]
        public int SilId { get; set; }

        [HasOne("organization")]
        public virtual Organization Organization { get; set; }
        public int OrganizationId { get; set; }

        [HasOne("role", Link.None)]
        public virtual Role Role { get; set; }
        public int RoleId { get; set; }

    }
}
