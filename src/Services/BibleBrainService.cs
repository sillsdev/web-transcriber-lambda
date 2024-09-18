using System.Diagnostics.Eventing.Reader;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services;

public class BibleBrainService
{
    private static readonly string _domain = "https://4.dbt.io/api/";
    private readonly HttpClient _client = new() { BaseAddress = new Uri(_domain) };
    private string _key = GetVarOrThrow("SIL_TR_BIBLEBRAIN");
    private async Task<string> DoApiCall(string uri)
    {
        HttpRequestMessage request = new (HttpMethod.Get, uri);
        request.Headers.Add("key", _key);
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
    public async Task<string> GetLanguages(bool withAudio = false)
    {
        string uri = "languages?v=4";
        if (withAudio)
        {
            uri += "&media=audio";
        }
        return await DoApiCall(uri);
    }
    public async Task<string> GetBibles(string lang, bool timingonly, int? limit = 50, int? page = 1 )
    {
        string uri = $"bibles?v=4&page={page}&limit={limit}";
        if (lang != "")
        {
            uri += $"&language_code={lang}";
        }
        if (timingonly)
        {
            uri += "&audio_timing=true";
        }
        return await DoApiCall(uri);
    }
}
