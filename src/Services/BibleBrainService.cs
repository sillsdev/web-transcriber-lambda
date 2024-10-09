using Newtonsoft.Json;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static SIL.Transcriber.Utility.Extensions.UriExtensions;
using SIL.Transcriber.Data;
using SIL.Transcriber.Utility;
using Newtonsoft.Json.Linq;
using System;


namespace SIL.Transcriber.Services;

public class BiblebrainPost
{
    public int PassageId { get; set; }
    public int? SectionId { get; set; }
    public int OrgWorkflowStep { get; set; }
    public string Bibleid { get; set; } = "";
    public bool Timing { get; set; } = false;
    public bool NT { get; set; } = true;
    public bool Sections { get; set; } = false;
    public bool Passages { get; set; } = false;
    public string Scope { get; set; } = "";
}
#pragma warning disable IDE1006 // Naming Styles
public class TimingData
{
    public string book { get; set; } = "";
    public string chapter { get; set; } = "";
    public string verse_start { get; set; } = "";
    public string verse_start_alt { get; set; } = "";
    public double timestamp { get; set; } = 0;
}
public class Region
{
    public double start { get; set; } = 0;
    public double end { get; set; } = 0;
}
public class RegionInfo
{
    public Region [] regions { get; set; } = Array.Empty<Region>();
}
public class Segment
{
    public string name { get; set; } = "";
    public Region[] regionInfo { get; set; } = Array.Empty<Region>();
}
#pragma warning restore IDE1006 // Naming Styles
public class GeneralResource
{
    public List<TimingData>Timing { get; set; }  = new();
    public int MediafileId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public double Duration { get; set; }
}


public class BibleBrainService : BaseResourceService
{
    private const string Domain = "https://4.dbt.io/api/";
    private const string Folder = "biblebrain/";
    private readonly HttpClient _client = new() { BaseAddress = new Uri(Domain) };
    private static string _key = GetVarOrThrow("SIL_TR_BIBLEBRAIN");
    readonly private HttpContext? HttpContext;
    public BibleBrainService(
       IHttpContextAccessor httpContextAccessor,
       AppDbContextResolver contextResolver,
       IS3Service s3Service) : base(contextResolver, s3Service)
    {
        HttpContext = httpContextAccessor.HttpContext;
    }
    private static List<(string Name, string Value)> AddParam(List<(string Name, string Value)> p, string Name, string? Value)
    {
        if ((Value ?? "") != "")
            p.Add((Name, Value ?? ""));
        return p;
    }
    private async Task<string> DoApiCall(string path, List<(string Name, string Value)>? myParams)
    {
        Uri uri = new($"{Domain}{path}");
        List<(string Name, string Value)>  p = new (myParams??new());
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
    /*
    public async Task<string> GetLanguages(string? country, string? languageCode, string? languageName, string? includeTranslations, string? l10n, string? page, string? limit)
    {
        List<(string Name, string Value)> p = new();
        AddParam(p, "media", "audio");
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
        AddParam(p, "media", media);
        AddParam(p, "page", page != null ? page.ToString() : "1");
        AddParam(p, "limit", limit != null ? limit.ToString() : "100");
        if (timingOnly)
            AddParam(p, "audio_timing", "true");

        return await DoApiCall("bibles", p);
    }
    */
    private Biblebrainfileset? CalcFileset(string bibleid, string testament, bool timing)
    {
        IQueryable<Biblebrainfileset> fs = DbContext.BibleBrainFilesets.Where(f => f.BibleId == bibleid && f.Timing == timing && f.FilesetSize == testament).OrderBy(f => f.MediaType);
        return fs.FirstOrDefault();
    }

    public async Task<string> GetCopyright(Biblebrainfileset? fs)
    {
        if (fs == null)
            return "";
        List <(string Name, string Value)> p = new();
        string fscr = await DoApiCall($"bibles/filesets/{fs.FilesetId}/copyright", p);
        //array of {id, type, size, copyright}
        dynamic? stuff = JsonConvert.DeserializeObject(fscr);
        return stuff?.copyright.copyright ?? "Unable to get copyright info";
    }
    public async Task<string> GetCopyright(string bibleid, string testament, bool timing)
    {
        return await GetCopyright(CalcFileset(bibleid, testament, timing));
    }
    private static string ContentType(Biblebrainfileset fileset, string url)
    {
        string contentType = "audio/mp3";
        if (fileset.Codec != null)
        {
            if (fileset.Codec == "opus")
                contentType = "audio/webm";
        }
        else
        {
            contentType = url.IndexOf(".mp3") > 0 ? "audio/mp3" : "audio/webm";
        }
        return contentType;
    }
    private async Task<GeneralResource> CreateGeneralResource(Biblebrainfileset fileset, string book, int chapter, int planId, string lang, string desc, int artifactTypeId, int? artifactCategoryId)
    {
        GeneralResource gr = new();
        string fs = await DoApiCall($"bibles/filesets/{fileset.FilesetId}/{book}/{chapter}", null);
        dynamic? stuff = JsonConvert.DeserializeObject(fs);
        if (stuff != null)
        {
            //upload files(s) to our folder
            JArray x = stuff.data;
            JToken data = x[0];
            string url = data["path"]?.ToString()??"";
            gr.Duration = (double)(data ["duration"] ?? 0);
            gr.ContentType = ContentType(fileset, url);
            gr.FileName = await UrlToS3(url, Folder);
            //create general resource mediafile
            Mediafile m = CreateMedia(gr.FileName, gr.ContentType, desc, null, planId, artifactTypeId,lang, gr.FileName, Folder, artifactCategoryId);
            gr.MediafileId = m.Id;
            //get timing file for this fileset
            fs = await DoApiCall($"timestamps/{fileset.FilesetId}/{book}/{chapter}", null);
            stuff = JsonConvert.DeserializeObject(fs);
            if (stuff != null)
            {
                //upload files(s) to our folder
                gr.Timing = stuff.data.ToObject<List<TimingData>>();
            }
        }
        return gr;
    }
    private static string Segments(double start, double end)
    {
        //[{ "name": "ProjRes", "regionInfo": "[{\"start\":428.6,\"end\":471.4}]"}]
        //[{"name": "ProjRes", "regionInfo": "{\"regions\":\"[{\\\"start\\\":13.572532580196208,\\\"end\\\":25.624781413788742},{\\\"start\\\":0,\\\"end\\\":13.572532580196208},{\\\"start\\\":25.624781413788742,\\\"end\\\":192.936}]\"}"}]
        Region r = new ()
        {
            start = start,
            end = end
        };
        Region[] value = { r };
        /*
        RegionInfo ri = new()
        {
            regions = value
        }; */
        Segment seg = new ()
        {
            name = "ProjRes",
            regionInfo = value
        };
        Segment [] segArray = { seg };
        return JsonConvert.SerializeObject(segArray);
    }
    private static double [] GetStartEnd (Passage p, List<TimingData> timing, double duration, int chapter)
    {
        int? startv = p.StartChapter == chapter ? p.StartVerse : 1;
        int? endv = p.EndChapter == chapter ? p.EndVerse : 1000;

        TimingData? startt =  timing.Find(t => t.verse_start == startv.ToString());
        TimingData? endt = timing.Find(t => t.verse_start == (endv+1).ToString());
        endt ??= timing.Last();
        double [] ret = { startt?.timestamp??0, endt?.timestamp??duration };
        return ret;
    }
    public async Task<string> Post(BiblebrainPost post)
    {
        List<int> mediaids = new();
        List<int> srids = new();
        Passage passage = DbContext.PassagesData.Where(p => p.Id == post.PassageId).FirstOrDefault() ?? throw new Exception("No Passage");
        int sectionId = (post.SectionId ?? passage?.SectionId) ?? throw new Exception("No SectionId");
        string fp = HttpContext != null ? HttpContext.GetFP() ?? "" : "";
        HttpContext?.SetFP("biblebrain");

        Section? section = DbContext.SectionsData.Where(s => s.Id == sectionId).FirstOrDefault();
        Artifacttype? artifacttype = DbContext.Artifacttypes.Where(a => a.Typename == "resource" && !a.Archived).FirstOrDefault();
        Artifactcategory? artifactcategory = DbContext.Artifactcategorys.Where(c => c.Categoryname == "scripture" && !c.Archived).FirstOrDefault();
        int lastseq = DbContext.Sectionresources.Where(sr => sr.SectionId == sectionId).OrderByDescending(sr => sr.SequenceNum).FirstOrDefault()?.SequenceNum ?? 0;
        //get filesets with timing for this bibleid
        Biblebrainbible? bible = DbContext.BibleBrainBibles.Where(b => b.BibleId == post.Bibleid).FirstOrDefault();
        Biblebrainfileset? fileset = CalcFileset(post.Bibleid, post.NT ? "NT" : "OT", post.Timing);
        if (fileset == null)
            return "no fileset";
        if (passage == null)
            return "no passage";
        int planId = section?.PlanId ?? 0;

        //not handling movements
        IQueryable<Passage> passages = post.Scope switch
        {
            "passage" => DbContext.PassagesData.Where(p => p.Id == post.PassageId),
            "section" => DbContext.PassagesData.Where(p => p.SectionId == post.SectionId && p.PassagetypeId == null).OrderBy(p => p.StartChapter).ThenBy(p => p.StartVerse),
            "book" => DbContext.Sections.Where(s => s.PlanId == planId)
                                                    .Join(DbContext.PassagesData, s => s.Id, p => p.SectionId, (s, p) => p).Where(p => p.PassagetypeId == null && p.Book == passage.Book).OrderBy(p => p.StartChapter).ThenBy(p => p.StartVerse),
            "chapter" => DbContext.Sections.Where(s => s.PlanId == planId)
                                                    .Join(DbContext.PassagesData, s => s.Id, p => p.SectionId, (s, p) => p).Where(p => p.PassagetypeId == null && p.Book == passage.Book &&
                                                    (p.StartChapter == passage.StartChapter || p.EndChapter == passage.StartChapter || p.StartChapter == passage.EndChapter || p.EndChapter == passage.EndChapter))
                                                    .OrderBy(p => p.StartChapter).ThenBy(p => p.StartVerse),
            _ => DbContext.PassagesData.Where(p => p.Id == 0),
        };
        ;
        List<int> sectionids = new();
        List<Passage> psgs = passages.ToList();
        int chapter = 0;
        string desc = bible?.BibleName??post.Bibleid;
        string lang = bible?.Iso??"eng";
        GeneralResource generalresource = new();
        TimingData[] timing = Array.Empty<TimingData>();
        for (int ix =  0; ix < psgs.Count; ix++)
        {
            Passage ps = psgs[ix];
            for (int chap = ps.StartChapter ?? 1; chap <= (ps.EndChapter ?? 1); chap++)
            {
                if (chap != chapter)
                {
                    chapter = chap;
                    generalresource = await CreateGeneralResource(fileset, ps.Book ?? "", chapter, planId, lang, desc, artifacttype?.Id ?? 0, artifactcategory?.Id);
                    mediaids.Add(generalresource.MediafileId);
                    if (!post.Timing)
                    {
                        //make it a general resource somehow...
                        Console.WriteLine("TODO");
                    }
                }
                if (post.Timing)
                {
                    if (post.Passages)
                    {
                        string mydesc = $"{desc} {ps.Reference}";
                        double [] times = GetStartEnd(ps, generalresource.Timing, generalresource.Duration, chapter);
                        string segments = Segments(times[0], times[1]);
                        Mediafile m = CreateMedia(generalresource.FileName, generalresource.ContentType, mydesc, ps.Id, planId, artifacttype?.Id ?? 0, lang, generalresource.FileName, Folder, artifactcategory?.Id, generalresource.MediafileId, segments);
                        mediaids.Add(m.Id);
                        srids.Add(CreateSR(mydesc, ++lastseq, m.Id, ps.SectionId, ps.Id, post.OrgWorkflowStep).Id);
                    }
                    if (post.Sections && sectionids.FindIndex(s => s == ps.SectionId) == -1)
                    {
                        IQueryable<Passage> mypsgs = passages.Where(p => p.SectionId == ps.SectionId && (p.StartChapter == chapter || p.EndChapter == chapter));
                        Passage startp = mypsgs.First();
                        Passage lastp = mypsgs.Last();
                        //THIS IS WRONG - FIND START VERSE OF CURRENT CHAPTER
                        int startChap = startp.StartChapter??1;
                        int endChap = lastp.EndChapter??1;
                        string end = startChap == endChap ?  $"{lastp.EndVerse}" : $"{endChap}:{lastp.EndVerse}";
                        string mydesc = $"{desc} {startChap}:{startp.StartVerse}-{end}";
                        double [] startv = GetStartEnd(startp, generalresource.Timing, generalresource.Duration, chapter);
                        double [] endv = GetStartEnd(lastp, generalresource.Timing, generalresource.Duration, chapter);
                        string segments = Segments(startv[0], endv[1]);
                        Mediafile m = CreateMedia(generalresource.FileName, generalresource.ContentType, mydesc, null, planId, artifacttype?.Id ?? 0, lang, generalresource.FileName, Folder, artifactcategory?.Id, generalresource.MediafileId,segments);
                        mediaids.Add(m.Id);
                        srids.Add(CreateSR(mydesc, ++lastseq, m.Id, ps.SectionId, null, post.OrgWorkflowStep).Id);
                        sectionids.Add(ps.SectionId);
                    }
                }
            }
        }
        HttpContext?.SetFP(fp);
        OrbitId[] ret = {
            new("mediafile", mediaids),
            new("sectionresource", srids)};

        return JsonConvert.SerializeObject(ret);
    }
}
