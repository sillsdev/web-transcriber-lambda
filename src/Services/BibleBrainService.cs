using System.Diagnostics.Eventing.Reader;
using System.Diagnostics.Metrics;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static SIL.Transcriber.Utility.Extensions.UriExtensions;

namespace SIL.Transcriber.Services;

public class BibleBrainService
{
    private const string Domain = "https://4.dbt.io/api/";
    private readonly HttpClient _client = new() { BaseAddress = new Uri(Domain) };
    private static string _key = GetVarOrThrow("SIL_TR_BIBLEBRAIN");
    private static List<(string Name, string Value)> AddParam(List<(string Name, string Value)> p, string Name, string? Value)
    {
        if ((Value ?? "") != "")
            p.Add((Name, Value ?? ""));
        return p;
    }
    private async Task<string> DoApiCall(string path, List<(string Name, string Value)> myParams)
    {
        Uri uri = new($"{Domain}{path}");
        List<(string Name, string Value)>  p = new (myParams);
        AddParam(p, "v", "4");
        AddParam(p, "key", _key);
        uri = uri.AddParameter(p.ToArray());
        Console.WriteLine("***URI***", uri);
        HttpRequestMessage request = new (HttpMethod.Get, uri);
        HttpResponseMessage response = await _client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }
        else
        {
            string error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                               $"HTTP Request error, Code: {response.StatusCode}, Content: {error}"
                                          );
        }
    }
    public async Task<string> GetLanguages(string? country, string? languageCode, string? languageName, string? includeTranslations, string? l10n, string? page, string? limit)
    {
        List<(string Name, string Value)> p = new();
        AddParam(p, "country", country);
        AddParam(p, "language_code", languageCode);
        AddParam(p, "language_name", languageName);
        AddParam(p, "includeTranslations", includeTranslations);
        AddParam(p, "l10n", l10n);
        AddParam(p, "page", page ?? "1");
        AddParam(p, "limit", limit ?? "100");

        return await DoApiCall("languages", p);
    }
    public async Task<string> GetBibles(string lang, string? media, bool timingOnly = false, int? page = 1, int? limit = 50)
    {
        List<(string Name, string Value)> p = new();
        AddParam(p, "language_code", lang);
        if (media != null)
            AddParam(p, "media", media);
        AddParam(p, "page", page != null ? page.ToString() : "1");
        AddParam(p, "limit", limit != null ? limit.ToString() : "100");

        return await DoApiCall("bibles", p);
    }
}
