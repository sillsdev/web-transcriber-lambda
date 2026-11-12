using System.Net.Http.Headers;

namespace SIL.Transcriber.Services;

public class HelloAOService
{
    private const string Domain = "https://bible.helloao.org/api/";

    private static async Task<string> SendRequest(string path)
    {
        using HttpClient client = new() { BaseAddress = new Uri(Domain) };
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        return response.IsSuccessStatusCode
            ? body
            : throw new HttpRequestException(
                $"HelloAO request failed: {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
                null,
                response.StatusCode
            );
    }

    public async Task<string> GetChapterAsync(string bibleId, string book, string chapter)
    {
        string path = $"{Uri.EscapeDataString(bibleId)}/{Uri.EscapeDataString(book)}/{Uri.EscapeDataString(chapter)}.simple.json";
        return await SendRequest(path);
    }
}
