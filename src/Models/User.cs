using System;
using System.Linq;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace SIL.Transcriber.Models
{
    public class User : BaseModel, IArchive
    {
        // Full Name of User
        // Comes from Auth0 (trusting that they handle correct order)
        [Attr]
        public string Name { get; set; }

        [Attr] //defaults to given-name
        public string GivenName { get; set; }

        [Attr("family-name")]
        public string FamilyName { get; set; }

        [Attr("email")]
        public string Email { get; set; }

        [Attr("phone")]
        public string Phone { get; set; }

        [Attr("timezone")]
        public string Timezone { get; set; }

        [Attr("locale")]
        public string Locale { get; set; }

        [Attr("is-locked")]
        public bool IsLocked { get; set; }

        [Attr("auth0Id")]
        public string ExternalId { get; set; }
        [Attr("sil-userid")]
        public int? SilUserid { get; set; }

        [Attr("identity-token")]
        public string identitytoken { get; set; }
        [Attr("uilanguagebcp47")]
        public string uilanguagebcp47 { get; set; }
        [Attr("timercount-up")]
        public Boolean? timercountup { get; set; }
        [Attr("playback-speed")]
        public int? playbackspeed { get; set; }
        [Attr("progressbar-typeid")]
        public int? progressbartypeid { get; set; }
        [Attr("avatar-url")]
        public string avatarurl { get; set; }
        [Column(TypeName = "jsonb")]
        [Attr("hot-keys")]
        public string hotkeys { get; set; } //json
        [Attr("digest-preference")]
        public int? DigestPreference { get; set; }
        [Attr("news-preference")]
        public bool? NewsPreference { get; set; }
        //[HasMany("owned-organizations")]
        //public virtual List<Organization> OwnedOrganizations { get; set; }

        //[HasManyThrough(nameof(OrganizationMemberships))]   these cause issues...don't use 
        //[HasMany("Organizations")]
        //public List<Organization> Organizations { get; set; }
        [HasMany("organization-memberships", Link.None)]
        public virtual List<OrganizationMembership> OrganizationMemberships { get; set; }

        [HasMany("group-memberships", Link.None)]
        public virtual List<GroupMembership> GroupMemberships { get; set; }
        
        [NotMapped]
        public IEnumerable<int> OrganizationIds => OrganizationMemberships?.Where(om => !om.Archived).Select(o => o.OrganizationId);

        [NotMapped]
        public IEnumerable<Organization> Organizations => OrganizationMemberships?.Where(om => !om.Archived).Select(o => o.Organization);

        [NotMapped]
        public IEnumerable<int> GroupIds => GroupMemberships?.Where(gm => !gm.Archived).Select(g => g.GroupId);

        [NotMapped]
        public IEnumerable<Group> Groups => GroupMemberships?.Where(gm => !gm.Archived).Select(g => g.Group);

        //[NotMapped]
        //public IEnumerable<Group> ProjectIds => GroupMemberships?.Select(g => g.Group);

        public bool HasOrgRole(RoleName role, int orgId)
        {
            OrganizationMembership omSuper;
            omSuper = OrganizationMemberships != null ? 
             OrganizationMemberships
            .Where(r => r.RoleName == RoleName.SuperAdmin)
            .FirstOrDefault() : null;

            if (omSuper != null)
                return true; //they have all the roles
            if (this.OrganizationMemberships is null) return false;
            return this.OrganizationMemberships.Where(r => r.OrganizationId == orgId && r.RoleName == role && !r.Archived).FirstOrDefault() != null;
        }
        public bool HasGroupRole(RoleName role, int groupid)
        {
            OrganizationMembership omSuper;
            omSuper = this
            .OrganizationMemberships
            .Where(r => r.RoleName == RoleName.SuperAdmin)
            .FirstOrDefault();

            if (omSuper != null)
                return true; //they have all the roles

            return this.GroupMemberships.Where(r => r.GroupId == groupid && r.RoleName == role && !r.Archived).FirstOrDefault() != null;

        }
        /*
        public string LocaleOrDefault()
        {
            var locale = "en-US";
            if (!String.IsNullOrEmpty(Locale))
            {
                locale = Locale;
            }
            else if ((CultureInfo.CurrentCulture != null) && !String.IsNullOrEmpty(CultureInfo.CurrentCulture.Name))
            {
                locale = CultureInfo.CurrentCulture.Name;
            }
            return locale;
        }
        */
        public bool Archived { get; set; }
    }
}
