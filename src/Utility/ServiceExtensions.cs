using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;

namespace SIL.Transcriber.Utility
{
    public static class ServiceExtensions
    {
        public static async Task<IEnumerable<T>> GetScopedToOrganization<T>(
            Func<Task<IEnumerable<T>>> baseQuery,
            IJsonApiContext jsonApiContext)
        {
            QuerySet query = jsonApiContext.QuerySet;
            string orgIdToFilterBy = "";

            if (query == null)
            {
                query = new QuerySet();
                jsonApiContext.QuerySet = query;
            }

            query.Filters.Add(new JsonApiDotNetCore.Internal.Query.FilterQuery("organization-header", orgIdToFilterBy, "eq"));

            return await baseQuery();

        }
        public static void RemoveScopedToCurrentUser(
                              IJsonApiContext jsonApiContext)
        {
            QuerySet query = jsonApiContext.QuerySet;
            if (query != null)
            {
                FilterQuery filter = query.Filters.Find(fq => fq.Attribute == "currentuser");
                if (filter != null)
                    query.Filters.Remove(filter);
            }

        }
        public static async Task<IEnumerable<T>> GetScopedToCurrentUser<T>(
                              Func<Task<IEnumerable<T>>> baseQuery,
                              IJsonApiContext jsonApiContext)
        {
            QuerySet query = jsonApiContext.QuerySet;

            if (query == null)
            {
                query = new QuerySet();
                jsonApiContext.QuerySet = query;
            }
            if (query.Filters.Find(fq => fq.Attribute == "currentuser") == null)
                query.Filters.Add(new JsonApiDotNetCore.Internal.Query.FilterQuery("currentuser", "currentuser", "eq"));

            return await baseQuery();
        }
        public static async Task<IEnumerable<T>> GetScopedToProjects<T>(
                      Func<Task<IEnumerable<T>>> baseQuery,
                      IJsonApiContext jsonApiContext,
                      int project)
        {
            QuerySet query = jsonApiContext.QuerySet;

            if (query == null)
            {
                query = new QuerySet();
                jsonApiContext.QuerySet = query;
            }
            FilterQuery f = query.Filters.Find(fq => fq.Attribute == "projectlist");
            if (f == null)
                query.Filters.Add(new JsonApiDotNetCore.Internal.Query.FilterQuery("projectlist", project.ToString(), "eq"));
            else
                f.Value = project.ToString();
            return await baseQuery();
        }

    }
}
