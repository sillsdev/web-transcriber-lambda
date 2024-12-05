using Newtonsoft.Json;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System.Net.Http.Headers;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services;

public class AeroService : BaseResourceService
{
    readonly private HttpContext? HttpContext;
    readonly private string Domain;
    private ILogger Logger { get; set; }
    public AeroService(
           IHttpContextAccessor httpContextAccessor,
           AppDbContextResolver contextResolver,
           ILoggerFactory loggerFactory,
           IS3Service s3Service) : base(contextResolver, s3Service)
    {
        HttpContext = httpContextAccessor.HttpContext;
        Logger = loggerFactory.CreateLogger("AeroService");
        Domain = GetVarOrThrow("SIL_TR_AERO_DOMAIN");
    }

    private async Task<string> GetToken()
    {
        using HttpClient httpClient = new();
        using MultipartFormDataContent content = new()
        {
            // Add username and password as string content
            { new StringContent("user_apm"), "username" },
            { new StringContent(GetVarOrThrow("SIL_TR_AERO_PASSWORD")), "password" }
        };
        HttpResponseMessage response = await httpClient.PostAsync($"{Domain}/token", content);
        dynamic? x = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
        return x?["access_token"] ?? "";
    }
    private async Task<HttpClient> Httpclient()
    {
        string token = await GetToken();
        HttpClient client = new ();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    private async Task<HttpResponseMessage> SendBinaryDataToApiAsync(Stream stream, string filename, string apiUrl)
    {
        byte[] data = ConvertStreamToByteArray(stream);
        using HttpClient httpClient = await Httpclient();
        // Prepare the multipart content
        using MultipartFormDataContent multipartContent = new();
        // Create the ByteArrayContent for the file
        ByteArrayContent fileContent = new (data);
        // Add the required headers for the file content
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        // Add the file content to the multipart form-data under the name 'file' to match FastAPI's expected parameter
        multipartContent.Add(fileContent, "file", filename);
        // Optionally, add other form data if needed (e.g., s3_upload boolean or user data)
        multipartContent.Add(new StringContent("true"), "s3_upload"); // Sends 's3_upload=True' in the request
                                                                      // Send the HTTP POST request to the FastAPI endpoint
        HttpResponseMessage response = await httpClient.PostAsync(apiUrl, multipartContent);
        response.EnsureSuccessStatusCode();
        return response;
    }
    public static byte[] ConvertStreamToByteArray(Stream stream)
    {
        using (MemoryStream memoryStream = new())
        {
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
    public async Task<string?> NoiseRemoval(IFormFile file)
    {
        using HttpClient httpClient = await Httpclient();
        // Prepare the multipart content
        using MultipartFormDataContent multipartContent = new();
        using Stream fileStream = file.OpenReadStream();
        using StreamContent fileContent = new (fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        // Add the file content to the multipart form-data under the name 'file' to match FastAPI's expected parameter
        multipartContent.Add(fileContent, "file", file.FileName);
        // Optionally, add other form data if needed (e.g., s3_upload boolean or user data)
        multipartContent.Add(new StringContent("true"), "s3_upload"); // Sends 's3_upload=True' in the request

        HttpResponseMessage response = await httpClient.PostAsync($"{Domain}/noise_removal", multipartContent);
        response.EnsureSuccessStatusCode();
        dynamic? x = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
        return x?["task_id"];
    }
    public async Task<string?> NoiseRemoval(string base64data, string filename)
    {
        byte[] fileBytes = Convert.FromBase64String(base64data);
        MemoryStream fileStream = new (fileBytes);
        return await NoiseRemoval(fileStream, filename);
    }
    public async Task<string?> NoiseRemovalS3(string fileUrl)
    {
        HttpClient client = new ();
        Uri uri = new (fileUrl);
        string filename = Path.GetFileName(uri.LocalPath);
        Stream fileStream = await client.GetStreamAsync(fileUrl);
        return await NoiseRemoval(fileStream, filename);
    }
    public async Task<string?> NoiseRemoval(Stream stream, string filename)
    {
        HttpResponseMessage response = await SendBinaryDataToApiAsync(stream, filename, $"{Domain}/noise_removal");
        dynamic? x = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
        return x?["task_id"];
    }
    public async Task<HttpContent?> NoiseRemovalStatus(string? TaskId)
    {
        using HttpClient httpClient = await Httpclient();
        HttpResponseMessage response = await httpClient.GetAsync($"{Domain}/noise_removal_status/{TaskId}");
        if (response.Headers.TryGetValues("task-state", out IEnumerable<string>? taskStates))
        {
            string? taskState = taskStates.FirstOrDefault();
            return taskState == "SUCCESS"
                ? response.Content
                : taskState == "FAILURE" ?
                    throw new Exception(taskState) :
                                null;
        }
        return null;
    }
    public async Task<string?> NoiseRemovalStatus(string? taskId, string outputFile, string outputFolder)
    {
        HttpContent? content = await NoiseRemovalStatus(taskId);
        if (content == null)
            return null;
        Stream stream = content.ReadAsStream();
        if (stream != null)
        {
            S3Response s3resp = await S3service.UploadFileAsync(stream, true, outputFile, outputFolder, true);
            return s3resp.Message;
        }
        return null;
    }
    public async Task<string?> VoiceConversion(Stream stream, string filename)
    {
        HttpResponseMessage returnData = await SendBinaryDataToApiAsync(stream, filename, $"{Domain}/voice_conversion");
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
    public async Task<string?> Transcription(Stream stream, string filename)
    {
        HttpResponseMessage returnData = await SendBinaryDataToApiAsync(stream,filename, $"{Domain}/voice_conversion");
        return "junk";
    }
    public async Task<string> TranscriptionStatus(string? TaskId)
    {
        using HttpClient httpClient = await Httpclient();
        HttpResponseMessage response = await httpClient.GetAsync($"{Domain}/voice_conversion_status/{TaskId}");
        return "Pending";
    }
}
