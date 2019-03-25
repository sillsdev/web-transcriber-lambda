﻿using System;
using System.Linq;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace SIL.Transcriber.Models
{
    public class User : BaseModel, ITrackDate
    {
        // Full Name of User
        // Comes from Auth0 (trusting that they handle correct order)
        [Attr("name")]
        public string Name { get; set; }

        [Attr("given-name")]
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

        [Attr("date-created")]
        public DateTime? DateCreated { get; set; }

        [Attr("date-updated")]
        public DateTime? DateUpdated { get; set; }

        //[HasMany("ownedOrganizations")]
        //public virtual List<Organization> OwnedOrganizations { get; set; }

        [HasMany("project-users")]
        public virtual List<ProjectUser> ProjectUsers { get; set; }

        [HasMany("organization-memberships", Link.None)]
        public virtual List<OrganizationMembership> OrganizationMemberships { get; set; }


        [HasMany("user-roles", Link.None)]
        public virtual List<UserRole> UserRoles { get; set; }


        [NotMapped]
        public IEnumerable<int> OrganizationIds => OrganizationMemberships?.Select(o => o.OrganizationId);
        /*
        public bool HasRole(RoleName role)
        {
            var userRole = this
                .UserRoles
                .Where(r => r.RoleName == role)
                .FirstOrDefault();

            return userRole != null;
        }
        */
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
    }
}
