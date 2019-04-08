﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;
using System.Linq;

namespace SIL.Transcriber.Models
{
    public partial class Organization : BaseModel
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("website-url")]
        public string WebsiteUrl { get; set; }

        [Attr("logo-url")]
        public string LogoUrl { get; set; }

        [Attr("public-by-default")]
        public bool? PublicByDefault { get; set; } = true;

        [HasOne("owner")]
        public virtual User Owner { get; set; }
        public int OwnerId { get; set; }
        
        [NotMapped]
        [HasManyThrough(nameof(OrganizationMemberships))]
        public List<User> Users { get; set; }
        public List<OrganizationMembership> OrganizationMemberships { get; set; }
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