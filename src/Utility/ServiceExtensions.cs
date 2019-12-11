using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            IJsonApiContext jsonApiContext)
        {
                 var query = jsonApiContext.QuerySet;
                var orgIdToFilterBy = "";

                if (query == null)
                {
                    query = new QuerySet();
                    jsonApiContext.QuerySet = query;
                }

                query.Filters.Add(new JsonApiDotNetCore.Internal.Query.FilterQuery("organization-header", orgIdToFilterBy, "eq"));

                return await baseQuery();

        }

        public static async Task<IEnumerable<T>> GetScopedToCurrentUser<T>(
                              Func<Task<IEnumerable<T>>> baseQuery,
                              IJsonApiContext jsonApiContext)
        {
            var query = jsonApiContext.QuerySet;

            if (query == null)
            {
                query = new QuerySet();
                jsonApiContext.QuerySet = query;
            }
            if (query.Filters.Find(fq => fq.Attribute == "currentuser") == null)
                query.Filters.Add(new JsonApiDotNetCore.Internal.Query.FilterQuery("currentuser", "currentuser", "eq"));

            return await baseQuery();
        }

    }
}
