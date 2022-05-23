﻿using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    [Table("users")]
    public class User: BaseModel, IArchive
        {
        public User(): base()
        {
            OrganizationMemberships = new List<OrganizationMembership>();
            GroupMemberships = new List<GroupMembership>();
        }
        // Full Name of User
        // Comes from Auth0 (trusting that they handle correct order)
            [Attr]
            public string Name { get; set; } = "";

            [Attr(PublicName = "given-name")]
            public string? GivenName { get; set; } = "";

            [Attr(PublicName = "family-name")]
            public string? FamilyName { get; set; } = "";

            [Attr]
            public string? Email { get; set; } = "";

            [Attr]
            public string? Phone { get; set; } = "";

            [Attr]
            public string? Timezone { get; set; } = "";

            [Attr]
            public string? Locale { get; set; } = "";

            [Attr(PublicName = "is-locked")]
            public bool IsLocked { get; set; }

            [Attr(PublicName = "auth0-id")]
            public string? ExternalId { get; set; } = "";

            [Attr(PublicName = "sil-userid")]
            public int? SilUserid { get; set; }

            [Attr(PublicName = "identity-token")]
            public string? IdentityToken { get; set; } = "";

            [Attr(PublicName = "uilanguagebcp47")]
            public string? UILanguageBCP47 { get; set; } = "";

            [Attr(PublicName = "timercount-up")]
            public bool? TimercountUp { get; set; }

            [Attr(PublicName = "playback-speed")]
            public int? PlaybackSpeed { get; set; }

            [Attr(PublicName = "progressbar-typeid")]
            public int? ProgressbarTypeid { get; set; }

            [Attr(PublicName = "avatar-url")]
            public string? AvatarUrl { get; set; } = "";

            [Column(TypeName = "jsonb")]
            [Attr(PublicName = "hot-keys")]
            public string? HotKeys { get; set; } = "{}"; //json;

            [Attr(PublicName = "digest-preference")]
            public int? DigestPreference { get; set; }
            [Attr(PublicName = "news-preference")]
            public bool? NewsPreference { get; set; }

            public bool Archived { get; set; }


        //[HasManyThrough(nameof(OrganizationMemberships))]   these cause issues...don't use 
            [JsonIgnore]
            [NotMapped]
            [HasMany]
            public ICollection<OrganizationMembership> OrganizationMemberships { get; set; }

            [JsonIgnore]
            [NotMapped]
            [HasMany]
            public ICollection<GroupMembership> GroupMemberships { get; set; }

       
            public bool HasOrgRole(RoleName role, int orgId)
            {
                OrganizationMembership? omSuper = OrganizationMemberships?.Where(r => r.RoleName == RoleName.SuperAdmin).FirstOrDefault();

                if (omSuper != null)
                    return true; //they have all the roles
                if (this.OrganizationMemberships is null) return false;
                return this.OrganizationMemberships.Where(r => r.OrganizationId == orgId && r.RoleName == role && !r.Archived).FirstOrDefault() != null;
            }
            public bool HasGroupRole(RoleName role, int groupid)
            {
                OrganizationMembership? omSuper = this
                .OrganizationMemberships?
                .Where(r => r.RoleName == RoleName.SuperAdmin)
                .FirstOrDefault();

                if (omSuper != null)
                    return true; //they have all the roles

            return GroupMemberships.Where(r => r.GroupId == groupid && r.RoleName == role && !r.Archived).FirstOrDefault() != null;

            }
            [NotMapped]
            public IEnumerable<int> OrganizationIds => OrganizationMemberships?.Where(om => !om.Archived).Select(o => o.OrganizationId) ?? new List<int>();

            [NotMapped]
            public IEnumerable<int> GroupIds => GroupMemberships?.Where(gm => !gm.Archived).Select(g => g.GroupId) ?? new List<int>();

    }
    public class CurrentUser : User
    {
        public CurrentUser(User user)
        {
            foreach (PropertyInfo userprop in typeof(User).GetProperties())
            {
                PropertyInfo? cuprop = typeof(CurrentUser).GetProperty(userprop.Name);
                if (cuprop != null)
                    cuprop.SetValue(this, userprop.GetValue(user, null), null);
            }
        }
    }

    }

