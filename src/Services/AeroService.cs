using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System.Net.Http.Headers;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services;

public class AeroService(
       IHttpContextAccessor httpContextAccessor,
       AppDbContextResolver contextResolver,
       ILoggerFactory loggerFactory,
       IS3Service s3Service) : BaseResourceService(contextResolver, s3Service)
{
    readonly private HttpContext? HttpContext = httpContextAccessor.HttpContext;
    readonly private string Domain = GetVarOrThrow("SIL_TR_AERO_DOMAIN");
    private ILogger Logger { get; set; } = loggerFactory.CreateLogger("AeroService");

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
    private static void AddSaveS3(MultipartFormDataContent content, bool upload)
    {
        content.Add(new StringContent(upload.ToString()), "s3_upload"); // Sends 's3_upload=True' in the request
    }

    private static MultipartFormDataContent AddFileToRequest(Stream stream, string filename, string param, MultipartFormDataContent? content = null)
    {
        byte[] data = ConvertStreamToByteArray(stream);
        // Prepare the multipart content
        MultipartFormDataContent multipartContent = content ?? [];
        // Create the ByteArrayContent for the file
        ByteArrayContent fileContent = new (data);
        // Add the required headers for the file content
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        // Add the file content to the multipart form-data under the name 'file' to match FastAPI's expected parameter
        multipartContent.Add(fileContent, param, filename);

        return multipartContent;
    }
    private async Task<HttpResponseMessage> SendBinaryDataToApiAsync(Stream stream, string filename, string apiUrl)
    {
        using HttpClient httpClient = await Httpclient();
        MultipartFormDataContent multipartContent = AddFileToRequest(stream, filename, "file");
        // Optionally, add other form data if needed (e.g., s3_upload boolean or user data)
        multipartContent.Add(new StringContent("true"), "s3_upload"); // Sends 's3_upload=True' in the request
                                                                      // Send the HTTP POST request to the FastAPI endpoint
        HttpResponseMessage response = await httpClient.PostAsync(apiUrl, multipartContent);
        response.EnsureSuccessStatusCode();
        return response;
    }
    private static byte[] ConvertStreamToByteArray(Stream stream)
    {
        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
    /*
    private static async void PrintContent(MultipartContent multipartContent)
    {
        foreach (HttpContent content in multipartContent)
        {
            if (content is StringContent stringContent)
            {
                string? name = content.Headers.ContentDisposition?.Name;
                string value = await stringContent.ReadAsStringAsync();
                Debug.WriteLine($"StringContent Name: {name}, Value: {value}");
            }
            else if (content is StreamContent streamcontent)
            {
                string? name = content.Headers.ContentDisposition?.Name;
                Debug.WriteLine($"StreamContent Name: {name}, Length: {content.Headers.ContentLength} , Type: {content.Headers.ContentType}");
            }
            else if (content is ByteArrayContent bcontent)
            {
                string? name = content.Headers.ContentDisposition?.Name;
                Debug.WriteLine($"ByteArrayContent Name: {name}, Length: {content.Headers.ContentLength} , Type: {content.Headers.ContentType}");
            }
        }
    }
    */
    private async Task<string?> GetTaskId(string api, MultipartFormDataContent multipartContent)
    {
        //PrintContent(multipartContent);
        using HttpClient httpClient = await Httpclient();
        HttpResponseMessage response = await httpClient.PostAsync(api, multipartContent);
        response.EnsureSuccessStatusCode();
        dynamic? x = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
        return x?["task_id"];
    }
    public async Task<string?> NoiseRemoval(Stream stream, string filename)
    {
        MultipartFormDataContent multipartContent = AddFileToRequest(stream, filename, "file");
        AddSaveS3(multipartContent, false);
        return await GetTaskId($"{Domain}/noise_removal", multipartContent);
    }
    public async Task<string?> NoiseRemoval(IFormFile file)
    {
        // Prepare the multipart content
        using Stream fileStream = file.OpenReadStream();
        return await NoiseRemoval(fileStream, file.FileName);
    }
    public async Task<string?> NoiseRemoval(string base64data, string filename)
    {
        byte[] fileBytes = Convert.FromBase64String(base64data);
        MemoryStream fileStream = new (fileBytes);
        return await NoiseRemoval(fileStream, filename);
    }
    public async Task<string?> NoiseRemoval(string fileUrl)
    {
        return await NoiseRemoval(await GetStream(fileUrl), GetFileName(fileUrl));
    }

    private async Task<HttpContent?> GetStatus(string service, string? TaskId)
    {
        using HttpClient httpClient = await Httpclient();
        HttpResponseMessage response = await httpClient.GetAsync($"{Domain}/{service}_status/{TaskId}");
        if (response.Headers.TryGetValues("task-state", out IEnumerable<string>? taskStates))
        {
            string? taskState = taskStates.FirstOrDefault();
            return taskState == "SUCCESS"
                ? response.Content
                : taskState == "FAILURE" ?
                    throw new Exception(taskState)
                : null;
        }
        return null;
    }
    public async Task<HttpContent?> NoiseRemovalStatus(string? taskId)
    {
        return await GetStatus("noise_removal", taskId);
    }
    public async Task<string?> NoiseRemovalStatus(string? taskId, string outputFile, string outputFolder)
    {

        Stream? stream = (await NoiseRemovalStatus(taskId))?.ReadAsStream();

        if (stream != null)
        {
            S3Response s3resp = await S3service.UploadFileAsync(stream, true, outputFile, outputFolder, true);
            return s3resp.Message;
        }
        return null;
    }
    private static string GetFileName(string sourceUrl)
    {
        Uri uri = new (sourceUrl);
        return Path.GetFileName(uri.LocalPath);
    }
    private static async Task<Stream> GetStream(string sourceUrl)
    {
        HttpClient client = new ();
        return await client.GetStreamAsync(sourceUrl);
    }
    private async Task<string?> VoiceConversion(Stream source, string sourcefilename, Stream target, string targetfilename)
    {
        MultipartFormDataContent multipartContent =  AddFileToRequest(source, sourcefilename, "source_file");
        AddFileToRequest(target, targetfilename, "target_file", multipartContent);
        AddSaveS3(multipartContent, false);
        return await GetTaskId($"{Domain}/voice_conversion", multipartContent);
    }
    public async Task<string?> VoiceConversion(string sourceUrl, string targetUrl)
    {
        return await VoiceConversion(await GetStream(sourceUrl), GetFileName(sourceUrl), await GetStream(targetUrl), GetFileName(targetUrl));
    }
    public async Task<HttpContent?> VoiceConversionStatus(string? taskId)
    {
        return await GetStatus("voice_conversion", taskId);
    }
    public async Task<string?> VoiceConversionStatus(string? taskId, string outputFile, string outputFolder)
    {

        Stream? stream = (await VoiceConversionStatus(taskId))?.ReadAsStream();

        if (stream != null)
        {
            S3Response s3resp = await S3service.UploadFileAsync(stream, true, outputFile, outputFolder, true);
            return s3resp.Message;
        }
        return null;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<string> TranscriptionLanguages()
    {
        using HttpClient httpClient = new();
        HttpResponseMessage response = await httpClient.GetAsync($"{Domain}/languages");
        string jsonString =  await response.Content.ReadAsStringAsync();
        JArray jsonArray = JArray.Parse(jsonString);
        JArray filteredArray = new (jsonArray.Where(item => item["is_mms_asr"]?.Value<bool>() ?? false));
        return filteredArray.ToString();

    }
    private async Task<string?> Transcription(Stream stream, string filename, string lang_iso, bool romanize)
    {
        string api = $"{Domain}/transcription?s3_upload=true&sister_lang_iso={lang_iso}&romanize={romanize}";
        MultipartFormDataContent content =  AddFileToRequest(stream, filename, "file");
        return await GetTaskId(api, content);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileUrl">The file containing the audio to transcribe.</param>
    /// <param name="lang_iso">The ISO code of the language to transcribe the audio into</param>
    /// <param name="romanize">Whether to romanize the transcription</param>
    /// <returns></returns>
    public async Task<string?> Transcription(string fileUrl, string lang_iso, bool romanize)
    {
        return await Transcription(await GetStream(fileUrl), GetFileName(fileUrl), lang_iso, romanize);
    }

    public async Task<TranscriptionResponse?> TranscriptionStatus(string? taskId)
    {
        HttpContent? content = await GetStatus("transcription", taskId);
        if (content != null)
        {
            dynamic? x = JsonConvert.DeserializeObject(await content.ReadAsStringAsync());
            if (x != null)
            {
                TranscriptionResponse response = new()
                {
                    Phonetic = x.result.phonetic_transcription ?? "",
                    Transcription =x.result.sister_transcription ?? "",
                    TranscriptionId = x.result.transcription_id ?? 0
                };
                return response;
            }
        };
        return null;
    }
}
public class FileUrlRequest
{
    public string FileUrl { get; set; } = "";
}
public class FileUploadModel
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Data { get; set; } = "";
}
public class SourceTargetRequest
{
    public string SourceUrl { get; set; } = "";
    public string TargetUrl { get; set; } = "";
}
public class TranscriptionRequest
{
    /// <summary>
    /// link to file
    /// </summary>
    public string FileUrl { get; set; } = "";
    public string Iso { get; set; } = "";
    public bool Romanize { get; set; } = true;
}
public class TranscriptionResponse
{
    public string Phonetic { get; set; } = ""; //The phonetic transcription of the audio.
    public string Transcription { get; set; } = ""; // The transcription of the audio in the target language.
    public int TranscriptionId { get; set; } // The ID of the transcription log entry.
}



