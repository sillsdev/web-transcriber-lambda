using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services;

public class AeroService : BaseResourceService
{
    private const string Domain = "https://aero-async.multilingualai.com";
    readonly private HttpContext? HttpContext;

    public AeroService(
           IHttpContextAccessor httpContextAccessor,
           AppDbContextResolver contextResolver,
           IS3Service s3Service) : base(contextResolver, s3Service)
    {
        HttpContext = httpContextAccessor.HttpContext;
    }

    private static async Task<string> GetToken()
    {
        using HttpClient httpClient = new();
        using (MultipartFormDataContent content = new ())
        {
            // Add username and password as string content
            content.Add(new StringContent("user_apm"), "username");
            content.Add(new StringContent(GetVarOrThrow("SIL_TR_AQUA_PASSWORD")), "password");
            HttpResponseMessage response = await httpClient.PostAsync($"{Domain}/token", content);
            dynamic? x = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
            return x?["access_token"]??"";
        }
    }
    private static async Task<HttpClient> Httpclient() {
        string token = await GetToken();
        HttpClient client = new ();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    private static async Task<HttpResponseMessage> SendBinaryDataToApiAsync(byte[] data, string filename, string apiUrl)
    {
        using HttpClient httpClient = await Httpclient();
        // Prepare the multipart content
        using MultipartFormDataContent multipartContent = new MultipartFormDataContent();
        // Create the ByteArrayContent for the file
        ByteArrayContent fileContent = new ByteArrayContent(data);
        // Add the required headers for the file content
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        // Add the file content to the multipart form-data under the name 'file' to match FastAPI's expected parameter
        multipartContent.Add(fileContent, "file", filename);
        // Optionally, add other form data if needed (e.g., s3_upload boolean or user data)
        multipartContent.Add(new StringContent("false"), "s3_upload"); // Sends 's3_upload=True' in the request
                                                                      // Send the HTTP POST request to the FastAPI endpoint
        HttpResponseMessage response = await httpClient.PostAsync(apiUrl, multipartContent);
        response.EnsureSuccessStatusCode();
        return response;
    }
    public async Task<string?> NoiseRemoval(byte[] data, string filename)
    {
        HttpResponseMessage response = await SendBinaryDataToApiAsync(data, filename, $"{Domain}/noise_removal");
        dynamic? x = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
        return x?["task_id"];
    }
    public async Task<string?> NoiseRemovalStatus(string? TaskId, string outputFile, string outputFolder)
    {
        using HttpClient httpClient = await Httpclient();
        HttpResponseMessage response = await httpClient.GetAsync($"{Domain}/noise_removal_status/{TaskId}");
        if (response.Headers.TryGetValues("task-state", out IEnumerable<string>? taskStates))
        {
            string? taskState = taskStates.FirstOrDefault();
            Console.WriteLine(taskState);
            if (taskState == "SUCCESS")
            {
                S3Response s3resp = await S3service.UploadFileAsync(response.Content.ReadAsStream(), true, outputFile, outputFolder, true);
                return s3resp.Message;
            }
            return taskState;
        }
        return null;
    }
    public async Task<string?> VoiceConversion(byte[] data, string filename)
    {
        HttpResponseMessage returnData = await SendBinaryDataToApiAsync(data, filename, $"{Domain}/voice_conversion");
        return "junk";
    }
    public async Task<string?> VoiceConversionStatus(string? TaskId)
    {
        using HttpClient httpClient = await Httpclient();
        HttpResponseMessage response = await httpClient.GetAsync($"{Domain}/voice_conversion_status/{TaskId}");
        return "Pending";
    }
    public async Task<int> TranscriptionLanguages()
    {
        using HttpClient httpClient = new();
        HttpResponseMessage response = await httpClient.GetAsync($"{Domain}/langugages");
        return 123;
    }
    public async Task<string?> Transcription(byte[] data, string filename)
    {
        HttpResponseMessage returnData = await SendBinaryDataToApiAsync(data,filename, $"{Domain}/voice_conversion");
        return "junk";
    }
    public async Task<string> TranscriptionStatus(string? TaskId)
    {
        using HttpClient httpClient = await Httpclient();
        HttpResponseMessage response = await httpClient.GetAsync($"{Domain}/voice_conversion_status/{TaskId}");
        return "Pending";
    }
}
