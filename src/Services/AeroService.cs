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
    private static ByteArrayContent GetFileContent(Stream stream)
    {
        byte[] data = ConvertStreamToByteArray(stream);
        // Create the ByteArrayContent for the file
        ByteArrayContent fileContent = new (data);
        // Add the required headers for the file content
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        /* this breaks it
        fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue(fileName)
        {
            FileName = fileName
        };
        */
        return fileContent;
    }

    private MultipartFormDataContent AddFileToRequest(Stream stream, string filename, string param, MultipartFormDataContent? content = null)
    {
        ByteArrayContent fileContent = GetFileContent(stream);
        // Prepare the multipart content
        MultipartFormDataContent multipartContent = content ?? [];
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

    private static async void PrintContent(MultipartContent multipartContent, ILogger Logger)
    {
        foreach (HttpContent content in multipartContent)
        {
            if (content is StringContent stringContent)
            {
                string? name = content.Headers.ContentDisposition?.Name;
                string value = await stringContent.ReadAsStringAsync();
                Logger.LogDebug("StringContent Name: {name}, Value: {value}", name, value);
            }
            else if (content is StreamContent streamcontent)
            {
                string? name = content.Headers.ContentDisposition?.Name;
                Logger.LogDebug("StreamContent Name: {n}, Length: {l} , Type: {t}", name, content.Headers.ContentLength, content.Headers.ContentType);
            }
            else if (content is ByteArrayContent bcontent)
            {
                string? name = content.Headers.ContentDisposition?.Name??content.Headers.ContentDisposition?.FileName;
                Logger.LogDebug("ByteArrayContent Name: {n}, Length: {l} , Type: {t}", name, content.Headers.ContentLength, content.Headers.ContentType);
            }
        }
    }
    private async Task<string[]?> GetTaskIds(string api, MultipartFormDataContent content)
    {
        string? tmp = await GetResult(api, content, "task_ids");
        string? result = tmp?.Replace("\"", "").Replace(" ", "").ReplaceLineEndings().Replace(Environment.NewLine, "").Trim('[', ']','"', ' ');
        return result?.Split(',');
    }


    private async Task<string?> GetResult(string api, MultipartFormDataContent multipartContent, string result)
    {
        Logger.LogCritical("GetResult");
        PrintContent(multipartContent, Logger);
        using HttpClient httpClient = await Httpclient();
        Logger.LogCritical("{api}", api);
        HttpResponseMessage response = await httpClient.PostAsync(new Uri(api), multipartContent);
        Logger.LogCritical("{s} {r}", response.StatusCode, response.ReasonPhrase);
        response.EnsureSuccessStatusCode();
        dynamic? x = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
        return x?[result].ToString();
    }
    public async Task<string?> NoiseRemoval(Stream stream, string filename)
    {
        MultipartFormDataContent multipartContent = AddFileToRequest(stream, filename, "file");
        AddSaveS3(multipartContent, true);
        return await GetResult($"{Domain}/noise_removal", multipartContent, "task_id");
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
            return taskState is "SUCCESS" or "finished"
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
    private async Task<Stream> GetStream(string sourceUrl)
    {
        HttpClient client = new ();
        Stream s = await client.GetStreamAsync(sourceUrl);
        return s;

    }
    private async Task<string?> VoiceConversion(Stream source, string sourcefilename, Stream target, string targetfilename)
    {
        MultipartFormDataContent multipartContent =  AddFileToRequest(source, sourcefilename, "source_file");
        AddFileToRequest(target, targetfilename, "target_file", multipartContent);
        AddSaveS3(multipartContent, false);
        return await GetResult($"{Domain}/voice_conversion", multipartContent, "task_id");
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
    private async Task<string[]?> Transcription(
        Stream stream, string filename, string lang_iso, bool romanize, float[]? timing = null)
    {
        string api = $"{Domain}/batch_transcription?s3_upload=true&sister_lang_iso={lang_iso}&romanize={romanize}";
        for (int ix = 0; ix < timing?.Length; ix++)
        {
            api += $"&timestamps={timing[ix]}";
        }

        MultipartFormDataContent content =  AddFileToRequest(stream, filename, "files");
        return await GetTaskIds(api, content);
    }
    public async Task<string[]?> Transcription(string[] fileUrls, string lang_iso, bool romanize)
    {
        string api = $"{Domain}/batch_transcription?s3_upload=true&sister_lang_iso={lang_iso}&romanize={romanize}";
        MultipartFormDataContent multipartContent = [];
        List<ByteArrayContent> files = [];
        int count = 1;
        foreach (string fileUrl in fileUrls)
        {
            Stream stream = await GetStream(fileUrl);
            string filename = GetFileName(fileUrl);
            AddFileToRequest(stream, count.ToString() + filename, "files", multipartContent);
            count++;
        }
        string[]? tasks = await GetTaskIds(api, multipartContent);
        return tasks;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileUrl">The file containing the audio to transcribe.</param>
    /// <param name="lang_iso">The ISO code of the language to transcribe the audio into</param>
    /// <param name="romanize">Whether to romanize the transcription</param>
    /// <param name="timing">verse timing</param>
    /// <returns></returns>
    public async Task<string[]?> Transcription(string fileUrl, string lang_iso, bool romanize, float[]? timing = null)
    {
        return await Transcription(await GetStream(fileUrl), GetFileName(fileUrl), lang_iso, romanize, timing);
    }

    public async Task<TranscriptionResponse?> TranscriptionStatus(string? taskId)
    {
        HttpContent? content = await GetStatus("batch_transcription", taskId);
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



