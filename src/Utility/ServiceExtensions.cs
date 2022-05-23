using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.QueryStrings;
using JsonApiDotNetCore.Services;

namespace SIL.Transcriber.Utility
{
    public static class ServiceExtensions
    {
        public static void RemoveScopedToCurrentUser(
                              IJsonApiOptions options)
        {
            //TODO 
/*
QuerySet query = options.QuerySet;
if (query != null)
{
    FilterQuery filter = query.Filters.Find(fq => fq.Attribute == "currentuser");
    if (filter != null)
        query.Filters.Remove(filter);
}
*/
}
public static async Task<IEnumerable<T>> GetScopedToCurrentUser<T>(
                  Func<Task<IReadOnlyCollection<T>>> baseQuery, IRequestQueryStringAccessor RequestQueryStringAccessor)
{
            /*
            var query = RequestQueryStringAccessor.Query;
            if (query.fil)
    QuerySet query = options.AllowUnknownFieldsInRequestBody

if (query == null)
{
query = new QuerySet();
options.QuerySet = query;
}
if (query.Filters.Find(fq => fq.Attribute == "currentuser") == null)
query.Filters.Add(new JsonApiDotNetCore.Internal.Query.FilterQuery("currentuser", "currentuser", "eq"));
*/
return await baseQuery();
        }
                /* unused I hope?
public static async Task<IReadOnlyCollection<T>> GetScopedToProjects<T>(
Func<Task<IReadOnlyCollection<T>>> baseQuery,
FilterExpression existingFilter,
int project)
{
    if (existingFilter == null)
    {
    query = new QuerySet();
}
FilterQuery f = query.Filters.Find(fq => fq.Attribute == "projectlist");
if (f == null)
query.Filters.Add(new JsonApiDotNetCore.Internal.Query.FilterQuery("projectlist", project.ToString(), "eq"));
else
f.Value = project.ToString();

return await baseQuery();
}
    */
}
}
