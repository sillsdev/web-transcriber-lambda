using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using System;


namespace SIL.Transcriber.Utility.Extensions.JSONAPI
{
   
    public static class FilterConstants
    {
        public const string ARCHIVED = "archived";
        public const string ALLOWED_CURRENTUSER = "currentuser";
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
    }
    public static class FilterQueryExtensions
    {
        public static string Field(this QueryExpression expression)
        {
            if (expression.GetType().IsAssignableFrom(typeof(ComparisonExpression)))
                return (expression as ComparisonExpression)?.Left.ToString() ?? "";
            return "";
        }
        public static string Value(this QueryExpression expression)
        {
            if (expression.GetType().IsAssignableFrom(typeof(ComparisonExpression)))
                return (expression as ComparisonExpression)?.Right.ToString() ?? "";
            return "";
        }
        public static string Operator(this QueryExpression expression)
        {
            if (expression.GetType().IsAssignableFrom(typeof(ComparisonExpression)))
                return (expression as ComparisonExpression)?.Operator.ToString() ?? ""; 
            return "";
        }
        public static bool Has(this QueryExpression expression, string param)
        {
            return (expression?.ToString() ?? "").Contains(param, StringComparison.OrdinalIgnoreCase);
        }

        public static string Field(this ExpressionInScope expression)
        {
            if (expression.Expression != null)
                return expression.Expression.Field();
            return "";
            
        }
        public static string Value(this ExpressionInScope expression)
        {
            if (expression.Expression != null)
                return expression.Expression.Value();
            return "";
        }
        public static bool Has(this ExpressionInScope expression, string param)
        {
            if (expression.Expression != null)
                return expression.Expression.Has(param);
            return false;
        }
    }
}