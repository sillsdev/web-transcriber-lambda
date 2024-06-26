using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;

namespace SIL.Transcriber.Utility.Extensions.JSONAPI
{
    public static class FilterConstants
    {
        public const string ARCHIVED = "archived";
        public const string PROJECT_LIST = "projectlist";
        public const string PROJECT_SEARCH_TERM = "project-id";

        public const string DATA_START_INDEX = "start-index";
        public const string PLANID = "plan-id";
        public const string IDLIST = "id-list";
        public const string PROJECT_UPDATED_DATE = "project-date";
        public const string VERSION = "version";
        public const string JSONFILTER = "json";
        public const string AUTH0ID = "auth0Id";
        public const string CURRENTUSER = "current";
        public const string ID = "id";
    }

    public static class FilterQueryExtensions
    {
        public static string Field(this QueryExpression expression)
        {
            return expression.GetType().IsAssignableFrom(typeof(ComparisonExpression))
                ? (expression as ComparisonExpression)?.Left.ToString() ?? ""
                : "";
        }

        public static string Value(this QueryExpression expression)
        {
            return expression.GetType().IsAssignableFrom(typeof(ComparisonExpression))
                ? (expression as ComparisonExpression)?.Right.ToString() ?? ""
                : "";
        }

        public static string Operator(this QueryExpression expression)
        {
            return expression.GetType().IsAssignableFrom(typeof(ComparisonExpression))
                ? (expression as ComparisonExpression)?.Operator.ToString() ?? ""
                : "";
        }

        public static bool Has(this QueryExpression expression, string param)
        {
            return (expression?.ToString() ?? "").Contains(
                "(" + param + ",",
                StringComparison.OrdinalIgnoreCase
            );
        }

        public static string Field(this ExpressionInScope expression)
        {
            return expression.Expression != null ? expression.Expression.Field() : "";
        }

        public static string Value(this ExpressionInScope expression)
        {
            return expression.Expression != null ? expression.Expression.Value() : "";
        }

        public static bool Has(this ExpressionInScope expression, string param)
        {
            return expression.Expression != null && expression.Expression.Has(param);
        }
    }
}
