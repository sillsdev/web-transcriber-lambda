using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;
using System.Linq;

namespace SIL.Transcriber.Models
{
    public partial class Group : BaseModel
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("abbreviation")]
        public string Abbreviation { get; set; }

        [HasOne("owner")]
        public virtual Organization Owner { get; set; }
        public int OwnerId { get; set; }

        [NotMapped]
        [HasManyThrough(nameof(GroupMemberships))]
        public List<User> Users { get; set; }
        public List<GroupMembership> GroupMemberships { get; set; }
        /*
         *      [NotMapped]
                [HasMany("userids")]
                public IEnumerable<int> UserIds => OrganizationMemberships?.Select(om => om.UserId);
                [NotMapped]
                [HasMany("users")]
                public IEnumerable<User> Users => OrganizationMemberships?.Select(om => om.User);
                /**/
    }
}
