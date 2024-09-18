using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static SIL.Transcriber.Utility.Extensions.UriExtensions;
using System.Text.Json;

namespace SIL.Transcriber.Services;

public class AquiferItem
{
    public string ContentId { get; set; } = "0";
    public string ContentType { get; set; } = "0";
}
public class AquiferService
{
    private static readonly string _domain = "https://api.aquifer.bible/";
    private readonly HttpClient _client = new() { BaseAddress = new Uri(_domain) };
    private string _key = GetVarOrThrow("SIL_TR_AQUIFER");
    private async Task<string> DoApiCall(string path, params (string Name, string Value) [] myparams)
    {
        Uri uri = new($"{_domain}{path}");
        if (myparams != null && myparams.Length > 0)
        {
            uri = uri.AddParameter(myparams);
            Console.WriteLine("***URI***", uri);
        }
        HttpRequestMessage request = new (HttpMethod.Get, uri);
        request.Headers.Add("api-key", _key);
        //request.Headers.Add("Accept", "application/json");

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
    public async Task<string> GetLanguages()
    {
        string uri = "languages";
        return await DoApiCall(uri);
    }
    private static List<(string Name, string Value)> AddParam(List<(string Name, string Value)> p, string Name, string? Value)
    {
        if ((Value??"") != "")
            p.Add((Name, Value??""));
        return p;
    }
    public async Task<string> Search(string bookCode, string languageCode, 
                                    string limit, string offset, 
                                    string? startChapter,
                                    string? startVerse,
                                    string? endChapter, 
                                    string? endVerse,
                                    string? query)
    {
        List<(string Name, string Value)> p = new()
        {
            ("bookCode", bookCode),
            ("languageCode", languageCode),
            ("limit", limit),
            ("offset", offset)
        };
        AddParam(p, "startChapter", startChapter);
        AddParam(p, "startVerse", startVerse);
        AddParam(p, "endChapter", endChapter);
        AddParam(p, "endVerse", endVerse);
        AddParam(p, "query", query);

        return await DoApiCall("resources/search", p.ToArray());
    }
    public async Task<string> GetContent(string contentid, string? type)
    {
        type ??= "0";
        return await DoApiCall($"resources/{contentid}", ("contentTextType", type));
    }
    public async Task<string> Post(AquiferItem [] content)
    {
        //List<AquiferItem>? items = JsonSerializer.Deserialize<List<AquiferItem>>(content);
        string info = "done";

        for (int ix = 0; ix < content?.Length; ix++) {
            AquiferItem c = content[ix];
            Console.WriteLine(c);
            info = await GetContent(c.ContentId, c.ContentType);
        };
        return info;
    }
}
