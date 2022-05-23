using System;
using System.Collections.Generic;
using System.Linq;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Utility
{
    public static class RepositoryExtensions
    {
        //FilterByOrganization can only be used by tables with organizationid

        public static IQueryable<T> GetAllInOrganizationIds<T>(this IQueryable<T> query, IEnumerable<int> orgIds) where T : IBelongsToOrganization, new()
        {
            return query.Where(p => orgIds.Contains(p.OrganizationId));
        }
        public static IQueryable<T> GetByOrganizationId<T>(this IQueryable<T> query, int organizationId) where T : IBelongsToOrganization, new()
        {
            return query.Where(p => p.OrganizationId == organizationId);
        }

//TODO
/*
        public static IQueryable<T> FilterByOrganization<T>(
            this IQueryable<T> query, 
            FilterQuery filterQuery,
            IEnumerable<int> allowedOrganizationIds
        ) where T : IBelongsToOrganization, new()
        {
            int specifiedOrgId;
            var hasSpecifiedOrgId = int.TryParse(filterQuery.Value, out specifiedOrgId);

            if (hasSpecifiedOrgId) {
                return query
                    .GetAllInOrganizationIds(allowedOrganizationIds)
                    .GetByOrganizationId(specifiedOrgId);
            }
            
            return query.GetAllInOrganizationIds(allowedOrganizationIds);
        }
*/
    }
}