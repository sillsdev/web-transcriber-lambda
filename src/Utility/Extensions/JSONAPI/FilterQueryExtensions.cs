using System;
using JsonApiDotNetCore.Internal.Query;


namespace SIL.Transcriber.Utility.Extensions.JSONAPI
{
    public static class FilterQueryExtensions
    {
        public static string ORGANIZATION_HEADER = "organization-header";
        public static string ALLOWED_CURRENTUSER = "currentuser";
        public static string PROJECT_LIST = "projectlist";
        public static string PROJECT_SEARCH_TERM = "project-id";
        public static string PROJECT_UPDATED_DATE = "project-updated-date";
        public static string DATA_START_INDEX = "start-index";
        public static string PLANID = "plan-id";
        public static string IDLIST = "id-list";
        public static string VERSION = "json";

        public static bool Has(this FilterQuery filterQuery, string param)
        {
          var attribute = filterQuery.Attribute;

          return attribute.Equals(param, StringComparison.OrdinalIgnoreCase);
        }
        public static bool HasSpecificOrg(this FilterQuery filterQuery)
        {
            return int.TryParse(filterQuery.Value, out int specifiedOrgId);
        }
    }
}