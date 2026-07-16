using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services.Contracts;
using System.Net.Http.Headers;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services;

public class AeroService(
       IHttpContextAccessor httpContextAccessor,
       AppDbContextResolver contextResolver,
       ILoggerFactory loggerFactory,
       IS3Service s3service) : BaseResourceService(contextResolver, s3service)
{
    readonly private HttpContext? HttpContext = httpContextAccessor.HttpContext;
    readonly private string Domain = GetVarOrThrow("SIL_TR_AERO_DOMAIN");
    readonly private string Bucket = GetVarOrThrow("SIL_TR_AERO_BUCKET");
    readonly private IS3Service s3Service = s3service;
    private const  string AERO_FOLDER = "input_files";
    private const string NOISE_REMOVAL = "v2/noise-removals";
    private const string VOICE_CONVERSION = "v2/voice-conversions";
    private const string AUDIO_INFILLING = "v2/audio-infillings";
    private const string TRANSCRIPTION = "v2/transcriptions";
    private const string PHONETIC = "v2/phonetic-transcriptions";
    private const string LANGUAGES = "v2/transcriptions/languages";
    private const string RECOMMENDATIONS = "v2/language-recommendations";

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
        string responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("Aero authentication failed: {Status} {Reason} - {Body}", response.StatusCode, response.ReasonPhrase, responseBody);
            throw new HttpRequestException($"Aero authentication failed: {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}", null, response.StatusCode);
        }
        dynamic? x = JsonConvert.DeserializeObject(responseBody);
        return x?["access_token"] ?? "";
    }
    private async Task<HttpClient> Httpclient()
    {
        string token = await GetToken();
        HttpClient client = new ();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    private static byte[] ConvertStreamToByteArray(Stream stream)
    {
        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    // multipart/form-data helpers removed — API now accepts JSON-only inputs

    private async Task<string?> GetResult(string api, HttpContent? content, string result)
    {
        Logger.LogDebug("GetResult {Api}", api);
        using HttpClient httpClient = await Httpclient();
        HttpResponseMessage response = await httpClient.PostAsync(new Uri(api), content);
        string responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("Aero API error [{Api}]: {Status} {Reason} - {Body}", api, response.StatusCode, response.ReasonPhrase, responseBody);
            throw new HttpRequestException($"Aero API returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}", null, response.StatusCode);
        }
        dynamic? x = JsonConvert.DeserializeObject(responseBody);
        string? ret = x?[result]?.ToString();
        return ret;
    }

    //if small enough to fit in the request
    public async Task<string?> NoiseRemoval(string base64data, string filename)
    {
        // Build JSON payload per new NoiseRemoval API (JSON-only)
        var payload = new
        {
            audio_base64 = base64data,
            audio_format = GetAudioFormatFromFilename(filename),
            s3_upload = true,
            method = "sam",
            prompt = "speech",
            quality = "high"
        };
        string json = JsonConvert.SerializeObject(payload);
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        return await GetResult($"{Domain}/{NOISE_REMOVAL}", content, "task_id");
    }

    //not small enough to fit in the request - send an s3 file that has been put in aero input_files
    public async Task<string?> NoiseRemoval(string fileName)
    {
        await S3service.BucketOwner(fileName, AERO_FOLDER, Bucket);
        // Build JSON payload with s3_path per new NoiseRemoval API
        var payload = new
        {
            s3_path = $"s3://{Bucket}/{AERO_FOLDER}/{fileName}",
            s3_upload = true,
            method = "sam",
            prompt = "speech",
            quality = "standard"
        };
        string json = JsonConvert.SerializeObject(payload);
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        return await GetResult($"{Domain}/{NOISE_REMOVAL}", content, "task_id");
    }


    public async Task<TaskStatusEnvelope?> NoiseRemovalStatus(string taskId)
    {
        return await GetStatus(NOISE_REMOVAL, taskId);
    }
    public async Task<string?> NoiseRemovalStatus(string taskId, string outputFile, string outputFolder)
    {
        TaskStatusEnvelope? env = await NoiseRemovalStatus(taskId);
        if (env == null)
            return null;

        if (!string.Equals(env.state, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            return null;

        string? audioUrl = env.result?["audio_url"]?.ToString();

        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            Logger.LogError("Noise removal result missing audio URL [{TaskId}]: {Json}", taskId, env.result?.ToString());
            return null;
        }

        return await FetchAndUploadAsync(audioUrl, outputFile, outputFolder);
    }
    public record TaskStatusEnvelope(string task_id, string state, JToken? result, JToken? error);

    private async Task<TaskStatusEnvelope> GetStatus(string service, string TaskId)
    {
        using HttpClient httpClient = await Httpclient();
        string url = $"{Domain}/{service}/{TaskId}";
        HttpResponseMessage response = await httpClient.GetAsync(url);
        string body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("Aero status check failed [{Service}/{TaskId}]: {Status} {Reason} - {Body}",
               service, TaskId, response.StatusCode, response.ReasonPhrase, body);
            throw new HttpRequestException($"Aero status check for '{service}' returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}", null, response.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            Logger.LogWarning("Aero status response empty [{Service}/{TaskId}]", service, TaskId);
            // Return an envelope with empty fields and PENDING state
            return new TaskStatusEnvelope(TaskId, "PENDING", null, null);
        }

        try
        {
            JObject json = JObject.Parse(body);
            string? state = json["state"]?.ToString();

            if (string.Equals(state, "FAILURE", StringComparison.OrdinalIgnoreCase))
            {
                string? err = json["error"]?["message"]?.ToString() ?? body;
                Logger.LogError("Aero task failed [{Service}/{TaskId}]: {Body}", service, TaskId, body);
                // Use HttpRequestException with no status parameter; controllers map exceptions to status codes
                throw new HttpRequestException($"Aero task failed: {err}");
            }

            JToken? result = json["result"] as JToken;
            JToken? error = json["error"] as JToken;
            return new TaskStatusEnvelope(json["task_id"]?.ToString() ?? TaskId, state ?? "PENDING", result, error);
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "Aero status response was not valid JSON [{Service}/{TaskId}]: {Body}", service, TaskId, body);
            throw;
        }
    }
    private static string GetFileName(string sourceUrl)
    {
        Uri uri = new (sourceUrl);
        return Path.GetFileName(uri.LocalPath);
    }
    private static string GetAudioFormatFromFilename(string filename)
    {
        string ext = Path.GetExtension(filename)?.ToLowerInvariant() ?? "";
        return ext switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".flac" => "audio/flac",
            _ => "application/octet-stream",
        };
    }
    // Wrap the response stream so disposing the returned Stream will also
    // dispose the HttpResponseMessage and HttpClient that created it.
    private class ResponseDisposingStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _response;
        private readonly HttpClient _client;
        private readonly MemoryStream? _prefix;

        public ResponseDisposingStream(Stream inner, HttpResponseMessage response, HttpClient client, byte[]? prefix = null)
        {
            _inner = inner;
            _response = response;
            _client = client;
            if (prefix != null && prefix.Length > 0)
                _prefix = new MemoryStream(prefix, writable: false);
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count)
        {
            return _prefix != null && _prefix.Position < _prefix.Length
                ? _prefix.Read(buffer, offset, count)
                : _inner.Read(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _prefix != null && _prefix.Position < _prefix.Length
                ? await _prefix.ReadAsync(buffer, offset, count, cancellationToken)
                : await _inner.ReadAsync(buffer, offset, count, cancellationToken);
        }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.WriteAsync(buffer, offset, count, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                { _inner.Dispose(); }
                catch { }
                try
                { _response.Dispose(); }
                catch { }
                try
                { _client.Dispose(); }
                catch { }
            }
            base.Dispose(disposing);
        }
    }

    private async Task<Stream> GetStream(string presignedGetUrl)
    {
        HttpClient client = new();
        HttpResponseMessage response = await client.GetAsync(presignedGetUrl, HttpCompletionOption.ResponseHeadersRead);
        Logger.LogDebug("GetStream: {Url} -> {Status}", presignedGetUrl, response.StatusCode);
        response.EnsureSuccessStatusCode();

        Stream sourceStream = await response.Content.ReadAsStreamAsync();
        Logger.LogDebug("GetStream: content-length={Length} canread={CanRead} canseek={CanSeek}", response.Content.Headers.ContentLength, sourceStream.CanRead, sourceStream.CanSeek);

        // Probe the first bytes so we can detect an empty body early and
        // also support underlying streams that have been advanced by the probe
        // by returning those bytes as a prefix to the returned stream.
        byte[] probe = new byte[4096];
        int bytesRead = 0;
        try
        {
            if (sourceStream.CanRead)
            {
                bytesRead = await sourceStream.ReadAsync(probe, 0, probe.Length);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "GetStream: probe read failed");
        }

        if (bytesRead > 0)
        {
            byte[] prefix = new byte[bytesRead];
            Array.Copy(probe, 0, prefix, 0, bytesRead);
            Logger.LogDebug("GetStream: probe read {Bytes} bytes", bytesRead);
            return new ResponseDisposingStream(sourceStream, response, client, prefix);
        }

        if (!sourceStream.CanRead || bytesRead == 0)
        {
            Logger.LogWarning("GetStream: response stream not readable or empty: content-length={Length} canread={CanRead}", response.Content.Headers.ContentLength, sourceStream.CanRead);
        }

        return new ResponseDisposingStream(sourceStream, response, client);
    }
    // Consolidated processing of v2 status envelopes returned by Aero APIs.
    private async Task<AeroResult> ProcessResult(HttpContent content, string service, string taskId)
    {
        string json = await content.ReadAsStringAsync();
        JObject obj = JObject.Parse(json);
        string? state = obj["state"]?.ToString();

        if (string.Equals(state, "FAILURE", StringComparison.OrdinalIgnoreCase))
        {
            string? err = obj["error"]?["message"]?.ToString() ?? "task failed";
            Logger.LogError("Aero task failed [{Service}/{TaskId}]: {Error}", service, taskId, err);
            return new AeroResult(false, null, err, json);
        }

        if (!string.Equals(state, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            // Not finished yet (PENDING/STARTED/etc.)
            return new AeroResult(false, null, null, json);
        }

        JToken? result = obj["result"] as JToken;
        string? audioUrl = result?["audio_url"]?.ToString()
            ?? result?["presigned_audio_url"]?.ToString()
            ?? result?["url"]?.ToString();

        return new AeroResult(true, audioUrl, null, json);
    }

    // Download the audio from a presigned URL and upload to S3. Returns S3 response message or null.
    private async Task<string?> FetchAndUploadAsync(string audioUrl, string outputFile, string outputFolder)
    {
        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            Logger.LogError("FetchAndUploadAsync called with empty audioUrl");
            return null;
        }

        using Stream stream = await GetStream(audioUrl);
        S3Response s3resp = await S3service.UploadFileAsync(stream, true, outputFile, outputFolder);
        return s3resp.Message;
    }
    private async Task<string?> VoiceConversion(Stream source, string sourcefilename, Stream target, string targetfilename)
    {
        if (source.CanSeek)
            source.Position = 0;
        if (target.CanSeek)
            target.Position = 0;

        byte[] sourceBytes = ConvertStreamToByteArray(source);
        byte[] targetBytes = ConvertStreamToByteArray(target);
        JObject payload = [];
        JObject sourceObj = new()
        {
            ["audio_base64"] = Convert.ToBase64String(sourceBytes),
            ["audio_format"] = GetAudioFormatFromFilename(sourcefilename)
        };
        payload["source"] = sourceObj;

        JObject targetObj = new()
        {
            ["audio_base64"] = Convert.ToBase64String(targetBytes),
            ["audio_format"] = GetAudioFormatFromFilename(targetfilename)
        };
        payload["target"] = targetObj;

        payload["s3_upload"] = true;
        payload["model"] = "SeedVC";
        string json = payload.ToString(Formatting.None);
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        return await GetResult($"{Domain}/{VOICE_CONVERSION}", content, "task_id");
    }

    public async Task<string?> VoiceConversion(string fileName, string targetUrl)
    {
        await S3service.BucketOwner(fileName, AERO_FOLDER, Bucket);
        string tgt = $"tgt{fileName}";
        await S3service.CopyS3FileAsync(targetUrl, Bucket, AERO_FOLDER, tgt);
        await S3service.BucketOwner(tgt, AERO_FOLDER, Bucket);
        JObject payload = new();
        JObject sourceObj = new()
        {
            ["s3_path"] = $"s3://{Bucket}/{AERO_FOLDER}/{fileName}"
        };
        payload["source"] = sourceObj;

        JObject targetObj = new()
        {
            ["s3_path"] = $"s3://{Bucket}/{AERO_FOLDER}/{tgt}"
        };
        payload["target"] = targetObj;

        payload["s3_upload"] = true;
        payload["model"] = "freevc";
        string json = payload.ToString(Formatting.None);
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        return await GetResult($"{Domain}/{VOICE_CONVERSION}", content, "task_id");
    }
    public async Task<TaskStatusEnvelope?> VoiceConversionStatus(string taskId)
    {
        return await GetStatus(VOICE_CONVERSION, taskId);
    }
    public async Task<string?> VoiceConversionStatus(string taskId, string outputFile, string outputFolder)
    {
        TaskStatusEnvelope? env = await VoiceConversionStatus(taskId);
        if (env == null)
            return null;

        if (!string.Equals(env.state, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            return null;

        string? audioUrl = env.result?["audio_url"]?.ToString()
            ?? env.result?["presigned_audio_url"]?.ToString()
            ?? env.result?["url"]?.ToString();

        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            Logger.LogError("Voice conversion result missing audio URL [{TaskId}]: {Json}", taskId, env.result?.ToString());
            return null;
        }

        return await FetchAndUploadAsync(audioUrl, outputFile, outputFolder);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<string> TranscriptionLanguages()
    {
        using HttpClient httpClient = new();
        HttpResponseMessage response = await httpClient.GetAsync($"{Domain}/{LANGUAGES}");
        string jsonString =  await response.Content.ReadAsStringAsync();
        JArray jsonArray = JArray.Parse(jsonString);
        JArray filteredArray = new (jsonArray.Where(item => item["is_mms_asr"]?.Value<bool>() ?? false));
        return filteredArray.ToString();

    }
    public async Task<string[]?> TranscriptionAsrMethods(string iso)
    {
        using HttpClient httpClient = new();
        HttpResponseMessage response = await httpClient.GetAsync($"{Domain}/{LANGUAGES}?language_iso={iso}");
        string jsonString = await response.Content.ReadAsStringAsync();
        // returns {  "languages": [{"iso": "eng", "name": "English"}], "entries": [{"language_iso": "eng", "script": "Latn", "method": "mms"},
        // {"language_iso": "eng", "script": "Latn", "method": "omnilingual"}, {"language_iso": "eng", "script": "Latn", "method": "whisper"}]}
        JObject jsonObject = JObject.Parse(jsonString);
        JArray? entries = jsonObject["entries"] as JArray;
        List<string> methods = entries?.Select(e => e["method"]?.Value<string>()).OfType<string>().Distinct().ToList() ?? [];

        string[] ranking = ["whisper", "w2v-bert", "omnilingual", "mms"];
        List<string> ranked = [.. ranking.Where(m => methods.Contains(m))];
        ranked.AddRange(methods.Where(m => !ranking.Contains(m)));

        return [.. ranked];
    }
    public async Task<string?> TranscriptionAsrSisters(string iso)
    {
        string api = $"{Domain}/{RECOMMENDATIONS}";
        var payload = new
        {
            iso
        };
        string json = JsonConvert.SerializeObject(payload);
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        return await GetResult(api, content, "task_id");
    }
    public async Task<string?> AsrSistersStatus(string taskId)
    {
        TaskStatusEnvelope? env = await GetStatus(RECOMMENDATIONS, taskId);
        if (env == null || !string.Equals(env.state, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            if (env.result != null)
            {
                string ret = JsonConvert.SerializeObject(env.result["result"] ?? env.result);
                return ret;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse transcription status response: {Json}", env.result?.ToString());
            throw;
        }
        return null;
    }
    private async Task<string[]?> Transcription(
        Stream stream, string filename, string lang_iso, bool romanize, float[]? timing = null)
    {
        // Build JSON payload using audio_clips for inline audio
        string api = $"{Domain}{TRANSCRIPTION}";

        if (stream.CanSeek)
            stream.Position = 0;
        byte[] bytes = ConvertStreamToByteArray(stream);
        JObject payload = new();

        JArray audioClips = new();
        JObject clip = new()
        {
            ["audio_base64"] = Convert.ToBase64String(bytes),
            ["audio_format"] = GetAudioFormatFromFilename(filename)
        };
        audioClips.Add(clip);
        payload["audio_clips"] = audioClips;

        payload["language_iso"] = lang_iso;
        // choose default method
        string? method = (await TranscriptionAsrMethods(lang_iso))?.FirstOrDefault();
        if (!string.IsNullOrEmpty(method))
            payload["method"] = method;

        payload["s3_upload"] = true;
        payload["romanize"] = romanize;

        if (timing != null && timing.Length > 0)
        {
            JArray timestamps = new();
            foreach (float t in timing)
                timestamps.Add(t);
            payload["timestamps"] = timestamps;
        }

        payload["timestamp_level"] = "chunk";
        payload["best_quality"] = false;

        string json = payload.ToString(Formatting.None);
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");

        string? tmp = await GetResult(api, content, "task_id");
        if (tmp == null)
            return null;
        string? result = tmp?.Replace("\"", "").Replace(" ", "").ReplaceLineEndings().Replace(Environment.NewLine, "").Trim('[', ']', '"', ' ');
        return result?.Split(',');
    }
    public async Task<string[]?> Transcription(string[] fileUrls, string lang_iso, bool romanize)
    {
        string api = $"{Domain}{TRANSCRIPTION}";
        // Copy files to S3 input folder and build s3_paths
        List<string> s3paths = new();
        int count = 1;
        foreach (string fileUrl in fileUrls)
        {
            Uri uri = new(fileUrl);
            string ext = Path.GetExtension(uri.LocalPath);
            string tgt = $"{count}{DateTime.Now.Ticks}{ext}";
            await S3service.CopyS3FileAsync(fileUrl, Bucket, AERO_FOLDER, tgt);
            await S3service.BucketOwner(tgt, AERO_FOLDER, Bucket);
            s3paths.Add($"s3://{Bucket}/{AERO_FOLDER}/{tgt}");
            count++;
        }

        JObject payload = new();
        if (s3paths.Count > 0)
        {
            JArray paths = [.. s3paths];
            payload["s3_paths"] = paths;
        }

        payload["language_iso"] = lang_iso;
        string? method = (await TranscriptionAsrMethods(lang_iso))?.FirstOrDefault();
        if (!string.IsNullOrEmpty(method))
            payload["method"] = method;

        payload["s3_upload"] = true;
        payload["romanize"] = romanize;
        payload["timestamp_level"] = "chunk";
        payload["best_quality"] = false;

        string json = payload.ToString(Formatting.None);
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        string? tmp = await GetResult(api, content, "task_id");
        if (tmp == null)
            return null;
        string? result = tmp?.Replace("\"", "").Replace(" ", "").ReplaceLineEndings().Replace(Environment.NewLine, "").Trim('[', ']', '"', ' ');
        return result?.Split(',');
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileUrls">The files containing the audio to transcribe.</param>
    /// <param name="lang_iso">The ISO code of the language to transcribe the audio into</param>
    /// <param name="romanize">Whether to romanize the transcription</param>
    /// <param name="method">The transcription method to use</param>
    /// <param name="phonetic">Whether to use phonetic transcription</param>
    /// <param name="timing">verse timing</param>
    /// <returns></returns>
    public async Task<string[]?> TranscriptionNew(string[] fileUrls, string lang_iso, bool romanize, bool phonetic, string? method = null, float[]? timing = null)
    {
        // Copy files to S3 and build payload
        List<string> s3paths = [];
        int count = 1;
        string fn = DateTime.Now.Ticks.ToString();
        foreach (string fileUrl in fileUrls)
        {
            Uri uri = new(fileUrl);
            string ext = Path.GetExtension(uri.LocalPath);
            string tgt = $"{count}{fn}{ext}";
            await S3service.CopyS3FileAsync(fileUrl, Bucket, AERO_FOLDER, tgt);
            await S3service.BucketOwner(tgt, AERO_FOLDER, Bucket);
            s3paths.Add($"s3://{Bucket}/{AERO_FOLDER}/{tgt}");
            count++;
        }

        string api = phonetic ? $"{Domain}/{PHONETIC}" : $"{Domain}/{TRANSCRIPTION}";

        JObject payload = [];
        if (s3paths.Count > 0)
        {
            JArray paths = [.. s3paths];
            payload["s3_paths"] = paths;
        }

        payload["language_iso"] = lang_iso;
        if (!string.IsNullOrEmpty(method))
        {
            if (phonetic)
                payload["guidance_method"] = method;
            else
                payload["method"] = method;
        }
        else
        {
            string? defaultMethod = (await TranscriptionAsrMethods(lang_iso))?.FirstOrDefault();
            if (!string.IsNullOrEmpty(defaultMethod))
            {
                if (phonetic)
                    payload["guidance_method"] = defaultMethod;
                else
                    payload["method"] = defaultMethod;
            }
        }

        payload["s3_upload"] = true;
        payload["romanize"] = romanize;
        if (timing != null && timing.Length > 0)
        {
            JArray timestamps = new();
            foreach (float t in timing)
                timestamps.Add(t);
            payload["timestamps"] = timestamps;
        }
        payload["timestamp_level"] = "chunk";
        payload["best_quality"] = false;

        string json = payload.ToString(Formatting.None);
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        string? tmp = await GetResult(api, content, "task_id");
        if (tmp == null)
            return null;
        string? result = tmp?.Replace("\"", "").Replace(" ", "").ReplaceLineEndings().Replace(Environment.NewLine, "").Trim('[', ']', '"', ' ');
        return result?.Split(',');
    }

    public async Task<TranscriptionResponse?> TranscriptionStatus(string taskId, bool phonetic)
    {
        TaskStatusEnvelope? env = await GetStatus(phonetic ? PHONETIC : TRANSCRIPTION, taskId);
        if (env == null || !string.Equals(env.state, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            JToken? firstResult = env.result?["items"]?[0] ?? env.result?[0];
            if (firstResult == null)
                return null;

            TranscriptionResponse response = new()
            {
                Transcription = firstResult?["transcription"]?["transcription"]?.ToString() ?? "",
                TranscriptionId = firstResult?["log_id"]?.Value<int>() ?? 0
            };
            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse transcription status response: {Json}", env.result?.ToString());
            throw;
        }
    }

    //if small enough to fit in the request
    public async Task<string?> AudioInfilling(string base64data, string filename, string? replacements = null)
    {
        // Build JSON payload per new AudioInfilling API
        JObject payload = new()
        {
            ["audio_base64"] = base64data,
            ["audio_format"] = GetAudioFormatFromFilename(filename)
        };

        if (!string.IsNullOrEmpty(replacements))
        {
            try
            {
                JToken rep = JToken.Parse(replacements);
                payload["replacements"] = rep;
            }
            catch (JsonException)
            {
                // If replacements is not valid JSON, send as raw string field
                payload["replacements"] = replacements;
            }
        }

        payload["s3_upload"] = true;

        string json = payload.ToString(Formatting.None);
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        return await GetResult($"{Domain}/{AUDIO_INFILLING}", content, "task_id");
    }

    /*public async Task<string?> AudioInfilling(string base64data, string filename, string? modifiedText = null,
    string? inputText = null, string? wordTimes = null, string[]? replacementAudioUrls = null, string? replacements = null)
    {
        byte[] fileBytes = Convert.FromBase64String(base64data);
        MemoryStream fileStream = new (fileBytes);
        MultipartFormDataContent multipartContent = AddFileToRequest(fileStream, filename, "file");

        // Add modified_text parameter if provided
        if (!string.IsNullOrEmpty(modifiedText))
            multipartContent.Add(new StringContent(modifiedText), "modified_text");

        // Add optional parameters
        if (!string.IsNullOrEmpty(inputText))
            multipartContent.Add(new StringContent(inputText), "input_text");

        if (!string.IsNullOrEmpty(wordTimes))
            multipartContent.Add(new StringContent(wordTimes), "word_times");

        if (!string.IsNullOrEmpty(replacements))
            multipartContent.Add(new StringContent(replacements), "replacements");

        // Handle replacement audio files
        if (replacementAudioUrls != null && replacementAudioUrls.Length > 0)
        {
            foreach (string audioUrl in replacementAudioUrls)
            {
                Stream audioStream = await GetStream(audioUrl);
                string audioFilename = GetFileName(audioUrl);
                AddFileToRequest(audioStream, audioFilename, "replacement_audio_files", multipartContent);
            }
        }

        AddSaveS3(multipartContent, true);
        return await GetResult($"{Domain}/{AUDIO_INFILLING}", multipartContent, "task_id");
    }
    */
    //not small enough to fit in the request - send an s3 file that has been put in aero input_files
    public async Task<string?> AudioInfilling(string fileName, string? modifiedText = null,
        string? inputText = null, string? wordTimes = null, string[]? replacementAudioUrls = null, string? replacements = null)
    {
        await S3service.BucketOwner(fileName, AERO_FOLDER, Bucket);

        JObject payload = new()
        {
            ["s3_path"] = $"s3://{Bucket}/{AERO_FOLDER}/{fileName}"
        };

        if (!string.IsNullOrEmpty(modifiedText))
            payload["modified_text"] = modifiedText;

        if (!string.IsNullOrEmpty(inputText))
            payload["input_text"] = inputText;

        if (!string.IsNullOrEmpty(wordTimes))
        {
            try
            {
                JToken wt = JToken.Parse(wordTimes);
                payload["word_times"] = wt;
            }
            catch (JsonException)
            {
                payload["word_times"] = wordTimes;
            }
        }

        // Process replacements: either provided JSON or build from replacementAudioUrls
        if (!string.IsNullOrEmpty(replacements))
        {
            try
            {
                JToken rep = JToken.Parse(replacements);
                payload["replacements"] = rep;
            }
            catch (JsonException)
            {
                payload["replacements"] = replacements;
            }
        }

        if ((replacementAudioUrls != null && replacementAudioUrls.Length > 0))
        {
            JArray reps = payload["replacements"] as JArray ?? new JArray();
            int count = 1;
            foreach (string audioUrl in replacementAudioUrls)
            {
                string audioFilename = GetFileName(audioUrl);
                string replName = $"repl{count}_{audioFilename}";
                await S3service.CopyS3FileAsync(audioUrl, Bucket, AERO_FOLDER, replName);
                await S3service.BucketOwner(replName, AERO_FOLDER, Bucket);

                JObject repObj = new()
                {
                    ["start"] = 0,
                    ["end"] = 0,
                    ["audio_filename"] = replName,
                    ["audio_s3_path"] = $"s3://{Bucket}/{AERO_FOLDER}/{replName}"
                };
                reps.Add(repObj);
                count++;
            }
            payload["replacements"] = reps;
        }

        payload["s3_upload"] = true;
        string json = payload.ToString(Formatting.None);
        using StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");
        return await GetResult($"{Domain}/{AUDIO_INFILLING}", content, "task_id");
    }

    public async Task<TaskStatusEnvelope?> AudioInfillingStatus(string taskId)
    {
        return await GetStatus(AUDIO_INFILLING, taskId);
    }

    public async Task<string?> AudioInfillingStatus(string taskId, string outputFile, string outputFolder)
    {
        TaskStatusEnvelope? env = await AudioInfillingStatus(taskId);
        if (env == null)
            return null;

        if (!string.Equals(env.state, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            return null;

        string? audioUrl = env.result?["audio_url"]?.ToString()
            ?? env.result?["presigned_audio_url"]?.ToString()
            ?? env.result?["url"]?.ToString();

        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            Logger.LogError("Audio infilling result missing audio URL [{TaskId}]: {Json}", taskId, env.result?.ToString());
            return null;
        }

        return await FetchAndUploadAsync(audioUrl, outputFile, outputFolder);
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
    public string Transcription { get; set; } = ""; // The transcription of the audio in the target language.
    public int TranscriptionId { get; set; } // The ID of the transcription log entry.
}
public class AudioInfillingRequest
{
    /// <summary>
    /// link to file
    /// </summary>
    public string FileUrl { get; set; } = "";
    /// <summary>
    /// The modified/target text for the audio (optional if ReplacementAudioFiles or Replacements are provided)
    /// </summary>
    public string? ModifiedText { get; set; }
    /// <summary>
    /// The original text of the audio (required if ModifiedText is provided)
    /// </summary>
    public string? InputText { get; set; }
    /// <summary>
    /// JSON list of word timestamps (optional)
    /// </summary>
    public string? WordTimes { get; set; }
    /// <summary>
    /// List of replacement audio file URLs (optional if InputText and ModifiedText are given)
    /// </summary>
    public string[]? ReplacementAudioFiles { get; set; }
    /// <summary>
    /// JSON list of AudioInfillingReplacement objects (optional)
    /// </summary>
    public string? Replacements { get; set; } //{"start": 1.0, "end": 2.0, "audio_base64": "..."}]
}
public class WordTime
{
    public string Word { get; set; } = "";
    public float Start { get; set; }
    public float End { get; set; }
}
public class AudioInfillingReplacement
{
    public float Start { get; set; }
    public float End { get; set; }
    public string AudioBase64 { get; set; } = "";
}
public class AudioInfillingFileUploadModel
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Data { get; set; } = "";
    /// <summary>
    /// The modified/target text for the audio (optional if ReplacementAudioFiles or Replacements are provided)
    /// </summary>
    public string? ModifiedText { get; set; }
    /// <summary>
    /// The original text of the audio (required if ModifiedText is provided)
    /// </summary>
    public string? InputText { get; set; }
    /// <summary>
    /// JSON list of word timestamps (optional)
    /// </summary>
    public string? WordTimes { get; set; }
    /// <summary>
    /// List of replacement audio file URLs (optional)
    /// </summary>
    public string[]? ReplacementAudioFiles { get; set; }
    /// <summary>
    /// JSON list of AudioInfillingReplacement objects (optional)
    /// </summary>
    public string? Replacements { get; set; }
}
public class AudioInfillingFileUploadModelPhase1
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Data { get; set; } = ""; //base64 audio data of original file
    public string Replacements { get; set; } = ""; //format {"start": 8.04,"end": 9.00, "audio_format": "audio/mpeg", "audio_base64": "//..."}

}

public record AeroResult(bool Success, string? AudioUrl, string? ErrorMessage, string RawJson);















