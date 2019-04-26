using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Utility.Extensions.JSONAPI
{
    public static class FilterQueryExtensions
    {
        public static string ORGANIZATION_HEADER = "organization-header";
        public static string PROJECT_SEARCH_TERM = "search-term";
        public static string PROJECT_UPDATED_DATE = "project-updated-date";


        public static bool Has(this FilterQuery filterQuery, string param)
        {
          var attribute = filterQuery.Attribute;

          return attribute.Equals(param, StringComparison.OrdinalIgnoreCase);
        }
    }
}