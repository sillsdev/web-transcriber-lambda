namespace SIL.Transcriber.Utility.Extensions;

public static class UriExtensions
{
    public static Uri AddParameter(this Uri uri, params (string Name, string Value) [] myparams)
    {
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        foreach (var (name, value) in myparams)
            query [name] = value;
        var uriBuilder = new UriBuilder(uri)
        {
            Query = query.ToString()
        };
        return uriBuilder.Uri;
    }
}
