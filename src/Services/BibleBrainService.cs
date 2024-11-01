using Newtonsoft.Json;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static SIL.Transcriber.Utility.Extensions.UriExtensions;
using static SIL.Transcriber.Utility.HttpContextHelpers;
using SIL.Transcriber.Data;
using SIL.Transcriber.Utility;

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

public class BibleBrainService : BaseResourceService
{
    private const string Domain = "https://4.dbt.io/api/";
    private const string Folder = "biblebrain/";
    private readonly HttpClient _client = new() { BaseAddress = new Uri(Domain) };
    private static string _key = GetVarOrThrow("SIL_TR_BIBLEBRAIN");
    readonly private HttpContext? HttpContext;
    readonly private ISQSService _SQSService;
    public BibleBrainService(
       IHttpContextAccessor httpContextAccessor,
       AppDbContextResolver contextResolver,
       ISQSService sqsService,
    IS3Service s3Service) : base(contextResolver, s3Service)
    {
        HttpContext = httpContextAccessor.HttpContext;
        _SQSService = sqsService;
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

    public async Task<int> GetMessageCount()
    {
        return await _SQSService.BBMessageCount();
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
    private static int[] CalcStartEnd(Passage startp, Passage lastp, int chapter)
    {
        int startverse = startp.StartChapter == chapter ? (startp.StartVerse??1) : 1;
        int endverse = lastp.EndChapter == chapter ? lastp.EndVerse??-1 : -1;
        int[] myArray = {startverse, endverse};
        return myArray;
    }
    private static string CalcDesc(int startverse, int endverse, string desc, int chapter)
    {
        string end = endverse == -1 ?  "-Ω" : startverse == endverse ? "" : $"-{endverse}";
        return $"{desc} {chapter}:{startverse}{end}";
    }
    private string CreateGeneralResourceRequest(Biblebrainfileset fileset, string book, int chapter, int planId, string lang, string desc, int artifactTypeId, int? artifactCategoryId, string token)
    {
        return _SQSService.SendBBGeneralMessage(fileset.FilesetId, fileset.Codec, book, chapter, planId, lang, desc, artifactTypeId, artifactCategoryId, token);
    }
    private string CreateResourceRequest(Biblebrainfileset fileset, string book, int chapter, int? psgId, int sectionId, int planId, string lang, string desc, int startverse, int endverse, int seq, int artifactTypeId, int? artifactCategoryId, int orgWorkflowStepId, string token)
    {
        return _SQSService.SendBBResourceMessage(fileset.FilesetId,book, chapter, psgId, sectionId, planId, lang, desc, startverse, endverse, seq, artifactTypeId, artifactCategoryId, orgWorkflowStepId, token);
    }
    public async Task<int> Post(BiblebrainPost post)
    {
        Passage passage = DbContext.PassagesData.Where(p => p.Id == post.PassageId).FirstOrDefault() ?? throw new Exception("No Passage");
        int sectionId = (post.SectionId ?? passage?.SectionId) ?? throw new Exception("No SectionId");
        string fp = HttpContext != null ? HttpContext.GetFP() ?? "" : "";
        HttpContext?.SetFP("biblebrain");

        Section? section = DbContext.SectionsData.Where(s => s.Id == sectionId).FirstOrDefault();
        Artifacttype? grartifacttype = DbContext.Artifacttypes.Where(a => a.Typename == (post.Timing ? "resource" : "projectresource") && !a.Archived).FirstOrDefault();
        Artifacttype ? artifacttype = DbContext.Artifacttypes.Where(a => a.Typename == "resource" && !a.Archived).FirstOrDefault();
        Artifactcategory? artifactcategory = DbContext.Artifactcategorys.Where(c => c.Categoryname == "scripture" && !c.Archived).FirstOrDefault();
        int lastseq = DbContext.Sectionresources.Where(sr => sr.SectionId == sectionId).OrderByDescending(sr => sr.SequenceNum).FirstOrDefault()?.SequenceNum ?? 0;
        //get filesets with timing for this bibleid
        Biblebrainbible? bible = DbContext.BibleBrainBibles.Where(b => b.BibleId == post.Bibleid).FirstOrDefault();
        Biblebrainfileset? fileset = CalcFileset(post.Bibleid, post.NT ? "NT" : "OT", post.Timing) ?? throw new Exception("no fileset");

        int planId = section?.PlanId ?? 0;
        string token = HttpContext != null ? await HttpContextHelpers.GetJWT(HttpContext) : "notoken";
        if (passage == null) //already threw above
            return 0;
        string book = passage.Book ?? "";
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
        int count = 0;
        List<int> sectionids = new();
        List<Passage> psgs = passages.ToList();
        string desc = bible?.BibleName??post.Bibleid;
        string lang = bible?.Iso??"eng";

        IEnumerable<int> schapters = psgs.Select(p => p.StartChapter??0).Distinct();
        IEnumerable<int> echapters = psgs.Select(p => p.EndChapter??0).Distinct();
        List<int> allChapters = schapters.Concat(echapters).Distinct().Where(c => c != 0).ToList();
        foreach (int chapter in allChapters)
        {
            count++;
            string id = CreateGeneralResourceRequest(fileset, book, chapter, planId, lang, desc, grartifacttype?.Id ?? 0, artifactcategory?.Id, token);
        }
        if (!post.Timing)
            return count;
        for (int ix =  0; ix < psgs.Count; ix++)
        {
            Passage ps = psgs[ix];
            for (int chapter = ps.StartChapter ?? 1; chapter <= (ps.EndChapter ?? 1); chapter++)
            {
                if (post.Passages)
                {
                    int[] se = CalcStartEnd(ps, ps, chapter);
                    string mydesc = CalcDesc(se[0], se[1], desc, chapter);
                    string pid = CreateResourceRequest(fileset, book, chapter, ps.Id, ps.SectionId, planId, lang, mydesc, se[0], se[1],  ++lastseq, artifacttype?.Id ?? 0, artifactcategory?.Id,  post.OrgWorkflowStep, token);
                    count++;
                }
                if (post.Sections && sectionids.FindIndex(s => s == ps.SectionId) == -1)
                {
                    List<Passage> mypsgs = passages.Where(p => p.SectionId == ps.SectionId && (p.StartChapter == chapter || p.EndChapter == chapter)).ToList();
                    if (mypsgs.Count > 1 || !post.Passages)
                    {
                            
                        Passage startp = mypsgs.First();
                        Passage lastp = mypsgs.Last();
                        int[] se = CalcStartEnd(startp, lastp, chapter);
                        string mydesc = CalcDesc(se[0], se[1], desc, chapter);
                        string sid = CreateResourceRequest(fileset, book, chapter, null, ps.SectionId, planId, lang, mydesc, se[0], se[1],  ++lastseq,artifacttype?.Id ?? 0, artifactcategory?.Id,  post.OrgWorkflowStep, token);
                        sectionids.Add(ps.SectionId);
                        count++;
                    }
                }
            }
        }
        HttpContext?.SetFP(fp);
        return count;
    }
}
