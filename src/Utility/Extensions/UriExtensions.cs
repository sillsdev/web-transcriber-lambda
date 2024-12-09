using System.Collections.Specialized;
using System.Web;

namespace SIL.Transcriber.Utility.Extensions;

public static class UriExtensions
{
    public static Uri AddParameter(this Uri uri, params (string Name, string Value) [] @params)
    {
        if (!@params.Any())
        {
            return uri;
        }
        NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);
        foreach ((string Name, string Value) param in @params)
            query [param.Name] = param.Value.Trim();

        UriBuilder uriBuilder = new (uri)
        {
            Query = query.ToString()
        };
        return uriBuilder.Uri;
    }
}
