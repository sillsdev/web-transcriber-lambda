﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Utility
{
    public static class ServiceExtensions
    {
        public static async Task<IEnumerable<T>> GetScopedToOrganization<T>(
            Func<Task<IEnumerable<T>>> baseQuery,
            IOrganizationContext organizationContext,
            IJsonApiContext jsonApiContext

        )
        {
            if (organizationContext.SpecifiedOrganizationDoesNotExist)
            {
                return Enumerable.Empty<T>().AsQueryable();
            }
            else
            {
                var query = jsonApiContext.QuerySet;
                var orgIdToFilterBy = "";

                if (query == null)
                {
                    query = new QuerySet();
                    jsonApiContext.QuerySet = query;
                }

                if (organizationContext.HasOrganization) 
                {
                    orgIdToFilterBy = organizationContext.OrganizationId.ToString();
                }
                query.Filters.Add(new JsonApiDotNetCore.Internal.Query.FilterQuery("organization-header", orgIdToFilterBy, "eq"));

                return await baseQuery();
            }

        }
    }
}
