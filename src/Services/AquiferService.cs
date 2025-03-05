using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System.IO.Compression;
using System.Net;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static SIL.Transcriber.Utility.Extensions.UriExtensions;

namespace SIL.Transcriber.Services;

public class AquiferItem
{
    public string ContentId { get; set; } = "0";
    public string ContentType { get; set; } = "0";
}
public class AquiferPost
{
    public int? PassageId { get; set; }
    public int? SectionId { get; set; }
    public int? OrgWorkflowStep { get; set; }
    public AquiferItem[]? Items { get; set; }
}
public class AquiferService(
       IHttpContextAccessor httpContextAccessor,
       AppDbContextResolver contextResolver,
       IS3Service s3Service) : BaseResourceService(contextResolver, s3Service)
{
    private const string Domain = "https://api.aquifer.bible/";
    private const string Folder = "aquifer/";
    private readonly string Key = GetVarOrThrow("SIL_TR_AQUIFER");
    readonly private HttpContext? HttpContext = httpContextAccessor.HttpContext;

    private async Task<string> DoApiCall(string path, params (string Name, string Value)[] myparams)
    {
        Uri uri = new Uri($"{Domain}{path}").AddParameter(myparams);

        HttpRequestMessage request = new (HttpMethod.Get, uri);
        request.Headers.Add("api-key", Key);
        //request.Headers.Add("Accept", "application/json");

        HttpResponseMessage response = await Client.SendAsync(request);
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
        if ((Value ?? "") != "")
            p.Add((Name, Value ?? ""));
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
        List<(string Name, string Value)> p =
        [
            ("bookCode", bookCode),
            ("languageCode", languageCode),
            ("limit", limit),
            ("offset", offset)
        ];
        AddParam(p, "startChapter", startChapter);
        AddParam(p, "startVerse", startVerse);
        AddParam(p, "endChapter", endChapter);
        AddParam(p, "endVerse", endVerse);
        AddParam(p, "query", query);

        return await DoApiCall("resources/search", [.. p]);
    }
    public async Task<string> GetContent(string contentid, string? type)
    {
        type ??= "0";
        return await DoApiCall($"resources/{contentid}", ("contentTextType", type));
    }

    public async Task<string> Post(AquiferPost post)
    {
        List<int> mediaids = [];
        List<int> srids = [];
        //List<AquiferItem>? items = JsonSerializer.Deserialize<List<AquiferItem>>(content);
        string info = "done";
        Passage? passage = DbContext.PassagesData.Where(p => p.Id == post.PassageId).FirstOrDefault();
        int? sectionId = (post.SectionId ?? passage?.SectionId) ?? throw new Exception("No SectionId");
        string fp = HttpContext != null ? HttpContext.GetFP() ?? "" : "";
        HttpContext?.SetFP("aquifer");

        Section? section = DbContext.SectionsData.Where(s => s.Id == sectionId).FirstOrDefault();
        Artifacttype? artifacttype = DbContext.Artifacttypes.Where(a => a.Typename == "resource").FirstOrDefault();
        int lastseq = DbContext.Sectionresources.Where(sr => sr.SectionId == sectionId).OrderByDescending(sr => sr.SequenceNum).FirstOrDefault()?.SequenceNum ?? 0;
        for (int ix = 0; ix < post.Items?.Length; ix++)
        {
            AquiferItem c = post.Items[ix];
            info = await GetContent(c.ContentId, c.ContentType);
            dynamic? stuff = JsonConvert.DeserializeObject(info);

            string? t = stuff?.grouping.mediaType.Value;
            string desc = stuff?.localizedName??"";
            switch (t)
            {
                case "Audio":
                {
                    string url = stuff?.content.mp3.url??"";
                    string contentType = url != "" ? "audio/mp3" : "audio/webm";
                    if (url == "")
                        url = stuff?.content.webm.url ?? "";

                    if (url.EndsWith(".zip"))
                    {
                        string zipName = Path.GetFileNameWithoutExtension(url);
                        using Stream responseStream = await Client.GetStreamAsync(new Uri(url));
                        using ZipArchive archive = new(responseStream);
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string entryName = Path.GetFileName(entry.FullName);
                            using Stream entryStream = entry.Open();
                            using MemoryStream entryFile = new();
                            await entryStream.CopyToAsync(entryFile);
                            entryFile.Seek(0, SeekOrigin.Begin);
                            S3Response s3 = await S3service.UploadFileAsync(entryFile, true, zipName+entryName, Folder);
                            if (s3.Status != HttpStatusCode.OK)
                                throw new Exception($"Error uploading to S3: {s3.Message}");
                            desc = $"{stuff?.localizedName} {Path.GetFileNameWithoutExtension(entryName)}";
                            Mediafile m = CreateMedia(zipName+entryName, contentType, desc, passage?.Id, section?.PlanId ?? 0, artifacttype?.Id ?? 0, (string)(stuff?.language.code ?? ""), s3.Message, Folder);
                            mediaids.Add(m.Id);
                            srids.Add(CreateSR(desc, ++lastseq, m.Id, sectionId ?? 0, passage?.Id, post.OrgWorkflowStep ?? 0).Id);
                        }
                        break;


                    }
                    else
                    {
                        string fileName = await UrlToS3(url, Folder);
                        Mediafile m = CreateMedia(fileName, contentType, desc, passage?.Id, section?.PlanId ?? 0, artifacttype?.Id ?? 0, (string)(stuff?.language.code ?? ""), fileName, Folder);
                        mediaids.Add(m.Id);
                        srids.Add(CreateSR(desc, ++lastseq, m.Id, sectionId ?? 0, passage?.Id, post.OrgWorkflowStep ?? 0).Id);
                    }
                    break;
                }
                case "Image":
                {
                    string url = stuff?.content.url ?? "";
                    string contentType = $"image/{Path.GetExtension(url)[1..]}";
                    string fileName = await UrlToS3(url, Folder);
                    Mediafile m = CreateMedia(fileName, contentType, desc, passage?.Id, section?.PlanId ?? 0, artifacttype?.Id ?? 0, (string)(stuff?.language.code ?? ""), fileName, Folder);
                    mediaids.Add(m.Id);
                    srids.Add(CreateSR(desc, ++lastseq, m.Id, sectionId ?? 0, passage?.Id, post.OrgWorkflowStep ?? 0).Id);
                }
                break;
                case "Text":
                {
                    int cnt = stuff?.content is JArray ?  ((JArray?)stuff?.content)?.Count??0 : 0;
                    for (int ic = 0; ic < cnt; ic++)
                    {
                        desc = $"{stuff?.localizedName} {(cnt > 1 ? (ic + 1).ToString() : "")}";
                        Mediafile m = CreateMedia((string)(stuff?.content[ic]??""),"text/markdown", desc, passage?.Id, section?.PlanId ?? 0, artifacttype?.Id ?? 0, (string)(stuff?.language.code??""), "", "");
                        mediaids.Add(m.Id);
                        Sectionresource sr = CreateSR(desc, ++lastseq, m.Id, sectionId??0, passage?.Id, post.OrgWorkflowStep??0);
                        srids.Add(sr.Id);
                    }
                }
                break;
                default:
                    Console.WriteLine(stuff?.grouping.mediaType);
                    break;
            }
        };
        HttpContext?.SetFP(fp);
        OrbitId[] ret = [
            new ("mediafile", mediaids),
            new ("sectionresource", srids)];

        return JsonConvert.SerializeObject(ret);
    }
}
