using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using TranscriberAPI.Utility.Extensions;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static SIL.Transcriber.Utility.Extensions.ObjectExtensions;
using static SIL.Transcriber.Utility.ResourceHelpers;

namespace SIL.Transcriber.Services
{
    class Tags
    {
        public string title = "";
        public string artist = "";
        public string album= "";
        public string cover="";
    }
    class Timing
    {
        public float start =  0;
        public string verse = "";
    }
    public class MediafileService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Mediafile> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        PlanRepository planRepository,
        PassageRepository passageRepository,
        IS3Service service,
        AeroService aeroService,
        MediafileRepository myRepository,
        IHttpContextAccessor httpContextAccessor,
        AppDbContextResolver contextResolver,
        ICurrentUserContext currentUserContext
        ) : BaseArchiveService<Mediafile>(
            repositoryAccessor,
            queryLayerComposer,
            paginationContext,
            options,
            loggerFactory,
            request,
            resourceChangeTracker,
            resourceDefinitionAccessor,
            myRepository
            )
    {
        private IS3Service S3service { get; } = service;
        private AeroService Aeroservice { get; } = aeroService;
        private PlanRepository PlanRepository { get; set; } = planRepository;
        private PassageRepository PassageRepository { get; set; } = passageRepository;
        private MediafileRepository MyRepository { get; set; } = myRepository;
        private readonly AppDbContext dbContext = (AppDbContext)contextResolver.GetContext();
        readonly private HttpContext? HttpContext = httpContextAccessor.HttpContext;
        private readonly ICurrentUserContext CurrentUserContext = currentUserContext;
        private readonly ILoggerFactory LoggerFactory = loggerFactory;
        private readonly IHttpContextAccessor HttpContextAccessor = httpContextAccessor;
        private class StatusInfo
        {
            public Mediafile? mediafile;
            public string s3File="";
            public string folder="";
        }

        public Mediafile? GetFromFile(int plan, string s3File)
        {
            IEnumerable<Mediafile> files = MyRepository.Get(); //bypass user check
            return files.FirstOrDefault(p => p.S3File == s3File && p.PlanId == plan && !p.Archived);
        }
        //This has only been tested with resource files.  Their segments are in a different format than other mediafiles
        public Mediafile? GetFromFile(int plan, string s3File, string segments)
        {
            IEnumerable<Mediafile> files = MyRepository.Get(); //bypass user check
            dynamic? s = JsonConvert.DeserializeObject(segments);
            if (s is null or not JArray)
                return GetFromFile(plan, s3File);
            dynamic? start = s[0].regionInfo?[0]?.start;
            dynamic? end = s[0].regionInfo?[0]?.end;
            IEnumerable<Mediafile> resources = files.Where(p => p.S3File == s3File && p.PlanId == plan && !p.Archived);
            foreach (Mediafile item in resources)
            {
                if (item.Segments != null)
                {
                    dynamic? i =  JsonConvert.DeserializeObject(item.Segments);
                    if (i is null or not JArray)
                        continue;
                    if (i[0].regionInfo is JArray && i[0].regionInfo[0]?.start == start && i[0].regionInfo[0]?.end == end)
                        return item;
                }
            }
            return null;
        }
        public IEnumerable<Mediafile>? WBTUpdate()
        {
            HttpContext?.SetFP("api");
            return MyRepository.WBTUpdate();
        }


        public string DirectoryName(Mediafile entity)
        {
            return entity.S3Folder?.TrimEnd('/') ?? PlanRepository.DirectoryName(entity.Plan?.Id ?? entity.PlanId);
        }

        public string? GetAudioUrl(Mediafile mf)
        {
            if (mf.S3File != null)
            {
                S3Response response = S3service.SignedUrlForPut(
                    mf.S3File,
                    DirectoryName(mf),
                    mf.ContentType ?? ""
                );
                return response.Message;
            }
            return null;
        }

        public async Task<string> GetNewFileNameAsync(Mediafile mf, string suffix = "")
        {
            return await S3service.GetFilename(DirectoryName(mf), mf.OriginalFile ?? "", (mf.S3Folder ?? "") != "" || mf.SourceMedia != null, suffix);
        }

        public IEnumerable<Mediafile> ReadyToSync(int PlanId, int artifactTypeId)
        {
            return MyRepository.ReadyToSync(PlanId, artifactTypeId);
        }
        public IEnumerable<Mediafile> PassageReadyToSync(int PassageId, int artifactTypeId)
        {
            return MyRepository.PassageReadyToSync(PassageId, artifactTypeId);
        }

        public Mediafile? GetFileSignedUrl(int id)
        {
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null)
                return null;
            if (mf.ResourcePassageId != null)
            {
                Mediafile? res = MyRepository.GetLatestShared((int)mf.ResourcePassageId);
                if (res != null && res.S3File != null)
                    mf.AudioUrl = S3service
                        .SignedUrlForGet(res.S3File, DirectoryName(res), res.ContentType ?? "")
                        .Message;
            }
            else
            {
                mf.AudioUrl = S3service
                    .SignedUrlForGet(mf.S3File ?? "", DirectoryName(mf), mf.ContentType ?? "")
                    .Message;
            }
            //_ = await UpdateAsync(id, mf, new CancellationToken());
            return mf;
        }

        public async Task<S3Response> GetFile(int id)
        {
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null || mf.S3File == null)
            {
                return new S3Response { Message = "", Status = HttpStatusCode.NotFound };
            }
            Plan? plan = PlanRepository.GetWithProject(mf.PlanId);
            string folder = mf.S3Folder ?? PlanRepository.DirectoryName(plan);
            if (
                mf.S3File.Length == 0
                || !(await S3service.FileExistsAsync(mf.S3File, folder))
            )
                return new S3Response
                {
                    Message = mf.S3File.Length > 0 ? mf.S3File : "",
                    Status = HttpStatusCode.NotFound
                };

            S3Response response = await S3service.ReadObjectDataAsync(
                mf.S3File ?? "",
                PlanRepository.DirectoryName(plan)
            );
            response.Message = mf.OriginalFile ?? "";
            return response;
        }

        public async Task<S3Response> DeleteFile(int id)
        {
            //delete the s3 file
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null || mf.S3File == null)
            {
                return new S3Response { Message = "", Status = HttpStatusCode.NotFound };
            }
            S3Response response = await S3service.RemoveFile(mf.S3File, DirectoryName(mf));
            return response;
        }
        public async Task<string> MakePublic(Mediafile? mf)
        {
            if (mf == null || mf.S3File == null)
            {
                return "";
            }
            S3Response response = await S3service.MakePublic(mf.S3File, DirectoryName(mf));
            return response.Message;
        }
        private AppDbContext GetMyOwnContext()
        {
            DbContextOptions<AppDbContext> options =
                    new DbContextOptionsBuilder<AppDbContext>()
                    .UseNpgsql(GetVarOrDefault("SIL_TR_CONNECTIONSTRING", ""))
                    .Options;
            return new AppDbContext(options,
                                   CurrentUserContext,
                                   HttpContextAccessor, LoggerFactory);
        }
        private async Task<Mediafile> Reload(Mediafile m, AppDbContext? context = null)
        {
            context ??= GetMyOwnContext();
            await context.Entry(m).ReloadAsync();
            return context.Entry(m).Entity;
        }
        public async Task<string> MakePublic(int id)
        {
            Mediafile? m = MyRepository.Get(id);
            if (m != null && m.S3File == null)
            {
                m = await Reload(m);
            }
            return await MakePublic(m);
        }
        public async Task<Mediafile?> PublishM(int id, Mediafile m)
        {
            return await Publish(id, m.PublishTo ?? "{}");
        }
        public async Task<Mediafile?> Publish(int id, string publishTo)
        {
            return await MyRepository.Publish(id, publishTo, true);
        }
        public Mediafile? GetLatest(int passageId)
        {
            return dbContext.MediafilesData
                .Where(mf => mf.PassageId == passageId && !mf.Archived)
                .OrderByDescending(mf => mf.VersionNumber)
                .FirstOrDefault();
        }
        public Mediafile? GetLatest(int passageId, int? typeId)
        {
            return dbContext.MediafilesData
                .Where(mf => mf.PassageId == passageId && mf.ArtifactTypeId == typeId && !mf.Archived)
                .OrderByDescending(mf => mf.VersionNumber)
                .FirstOrDefault();
        }

        public string EAF(Mediafile mf)
        {
            string eaf = "";
            if (!string.IsNullOrEmpty(mf.Transcription))
            {
                //get the project language
                Plan? plan = PlanRepository.GetWithProject(mf.PlanId);
                string lang = !string.IsNullOrEmpty(plan?.Project.Language)
                    ? plan.Project.Language
                    : "en";
                string pattern = "([0-9]{1,2}:[0-9]{2}(:[0-9]{2})?)";

                eaf = LoadResource("EafTemplate.xml");
                XElement eafContent = XElement.Parse(eaf);
                //var sDebug = TraverseNodes(eafContent, 1);
                XElement elem;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                eafContent.Attribute("DATE").Value = DateTime.Now.ToUniversalTime().ToString();
                eafContent.GetElement("TIER").Attribute("DEFAULT_LOCALE").Value = lang;
                eafContent.GetElement("LOCALE").Attribute("LANGUAGE_CODE").Value = lang;
                eafContent.GetElement("HEADER").Attribute("MEDIA_FILE").Value = mf.S3File ?? "";
                eafContent.GetElement("MEDIA_DESCRIPTOR").Attribute("MEDIA_URL").Value =
                    mf.S3File ?? "";
                eafContent.GetElement("MEDIA_DESCRIPTOR").Attribute("MIME_TYPE").Value =
                    mf.ContentType ?? "";
                elem = eafContent.GetElementsWithAttribute("TIME_SLOT", "ts2").First();
                elem.Attribute("TIME_VALUE").Value = (mf.Duration ?? 0 * 1000).ToString();
                eafContent.GetElement("ANNOTATION_VALUE").Value = Regex.Replace(
                    HttpUtility.HtmlEncode(mf.Transcription),
                    pattern,
                    ""
                ); //TEST THE REGEX
                eaf = eafContent.ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }

            return eaf;
        }

        public async Task<Mediafile?> UpdateFileInfoAsync(int id, long filesize, decimal duration)
        {
            //where did you go?
            Mediafile? p = MyRepository.Get(id);
            if (p == null)
                return null;
            p.Filesize = filesize;
            p.Duration = (int)duration;
            _ = await base.UpdateArchivedAsync(id, p, new CancellationToken());
            return p;
        }

        public override async Task<Mediafile?> CreateAsync(
            Mediafile resource,
            CancellationToken cancellationToken
        )
        {
            //do this here because we know how to do it here...
            //but then capture it in the repository before it gets lost
            if (!resource.ContentType?.StartsWith("text") ?? true)
            {
                resource.S3File = GetNewFileNameAsync(resource).Result;
                resource.AudioUrl = GetAudioUrl(resource);
            }
            return await base.CreateAsync(resource, cancellationToken);
        }
        public List<Mediafile> GetIPMedia(IQueryable<Organization> orgs)
        {
            IEnumerable<Intellectualproperty>? ip = [.. dbContext.IntellectualPropertys.Join(orgs, ip => ip.OrganizationId, o => o.Id, (ip, o) => ip)];
            return ip.Join(dbContext.Mediafiles, ip => ip.ReleaseMediafileId, m => m.Id, (ip, m) => m).ToList();
        }


        #region AI
        private Mediafile? CreateNewMediafile(StatusInfo info)
        {
            Mediafile newmf = new ();
            info.mediafile?.CopyProperties(newmf);
            newmf.S3File = info.s3File;
            newmf.S3Folder = info.folder;
            int lastVers =  GetLatest(info.mediafile?.PassageId??0, info.mediafile?.ArtifactTypeId)?.VersionNumber??0;
            newmf.VersionNumber = ++lastVers;
            newmf.DateCreated = null;
            newmf.DateUpdated = null;
            dbContext.Mediafiles.Add(newmf);
            dbContext.SaveChanges();
            return newmf;
        }
        private StatusInfo GetStatusInfo(int id, string postfix)
        {
            StatusInfo ret = new ();
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null)
                return ret;
            string nextVers="";
            if (mf.PassageId != null)
            {
                Mediafile? ver = GetLatest(mf.PassageId??0, null);
                if (ver != null)
                    nextVers = ((ver.VersionNumber ?? 0) + 1).ToString();
            }

            Plan? plan = PlanRepository.GetWithProject(mf.PlanId);
            ret.mediafile = mf;
            ret.folder = mf.S3Folder ?? PlanRepository.DirectoryName(plan);
            ret.s3File = $"{mf.S3File?[..(mf.S3File.Length - Path.GetExtension(mf.S3File)?.Length ?? 0)]}{nextVers}{postfix}{Path.GetExtension(mf.S3File)}";
            return ret;
        }
        private async Task SaveTasks(Mediafile[] mfs, string taskName, string[] taskIds)
        {
            if (mfs.Length != taskIds.Length)
                throw new Exception($"Save Tasks mediafiles {mfs.Length} tasks {taskIds.Length}");
            for (int cnt = 0; cnt < mfs.Length; cnt++)
            {
                await SaveTask(mfs[cnt], taskName, taskIds[cnt]);
            }
        }
        private async Task UpdateSegments(Mediafile mf, string title, JObject segment, JObject ri, JArray regions)
        {
            dynamic? segments = JsonConvert.DeserializeObject(mf.Segments??"{}");
            JArray newsegments = [];

            if (segments is not JArray)
            {
                newsegments.Add(segment);
            }
            else
            {
                //deserialize everything
                foreach (dynamic? seg in segments)
                {
                    dynamic? regionInfo = seg.regionInfo;
                    if (regionInfo.Type == JTokenType.String)
                    {
                        regionInfo = JsonConvert.DeserializeObject(regionInfo.ToString());
                        seg.regionInfo = regionInfo;
                    }
                    if (regionInfo != null)
                    {
                        dynamic? newregions = regionInfo.regions;
                        if (newregions?.Type == JTokenType.String)
                        {
                            regionInfo.regions = JsonConvert.DeserializeObject(newregions?.ToString());
                        }
                    }
                    newsegments.Add(seg);
                }
                List<JToken> oldtasks = newsegments.Where(x => x is JObject j && j.ContainsKey("name") && j["name"]?.Value<string>() == title).ToList();
                if (oldtasks.Count > 0)
                {
                    dynamic oldtask = oldtasks.First();
                    dynamic? oldri = oldtask.regionInfo;
                    if (oldri == null)
                        oldtask.regionInfo = ri;
                    else
                        oldri.regions = regions;
                }
                else
                {
                    (newsegments).Add(segment);
                }
            }
            mf.Segments = JsonConvert.SerializeObject(newsegments);
            await UpdateAsync(mf.Id, mf, new CancellationToken());
        }
        private async Task SaveTasks(Mediafile mf, string taskName, List<Timing> times, string[] taskIds)
        {
            if (times.Count != taskIds.Length)
                throw new Exception($"Save Tasks times {times.Count} tasks {taskIds.Length}");
            string title = taskName+"Task";
            List<JObject> regions = [];
            int cnt = 0;
            foreach (Timing timing in times)
            {
                regions.Add(new(new JProperty("start", timing.start), new JProperty("end", 0), new JProperty("label", $"{taskIds[cnt++]}|{timing.verse}")));
            }
            JObject ri = new(new JProperty("regions", regions));
            JObject segment = new(new JProperty("name", title), new JProperty("regionInfo", ri));
            await UpdateSegments(mf, title, segment, ri, [.. regions]);
        }
        private static List<Timing>? GetVerseTiming(Mediafile mf)
        {
            dynamic? segments = JsonConvert.DeserializeObject(mf.Segments??"{}");
            if (segments is null or not JArray)
                return null;
            dynamic? verse = ((JArray)segments).Where(x => x is JObject j && j.ContainsKey("name") && j["name"]?.Value<string>() == "Verse").ToList().FirstOrDefault();
            if (verse != null)
            {
                dynamic? ri = verse.regionInfo;
                if (ri.Type == JTokenType.String)
                    ri = JsonConvert.DeserializeObject(ri.ToString());
                dynamic? regions = ri?.regions;
                if (regions == null)
                    return null;
                if (regions.Type == JTokenType.String)
                    regions = JsonConvert.DeserializeObject(regions.ToString());
                //array of {start,end,label}
                List<Timing> timing = [];
                if (regions is JArray)
                {
                    foreach (dynamic region in regions)
                    {
                        string s = region.label.Value.ToString();
                        string[] parts = s.Split(':');
                        string v = parts.Length > 1 ? parts[1] : parts[0];
                        timing.Add(new Timing() { start = (float)region.start.Value, verse = v });
                    }
                    return timing;
                }
            }
            return null;
        }
        private async Task SaveTask(Mediafile mf, string taskName, string id)
        {
            string title = taskName+"Task";
            //string str = $"{{\"{title}\":\"{id}\"}}";
            JObject newtask = new(new JProperty("start", 0), new JProperty("end", 0), new JProperty("label", id));
            JArray regions = [newtask];
            JObject ri = new(new JProperty("regions", regions));
            JObject segment = new(new JProperty("name", title), new JProperty("regionInfo", ri));
            await UpdateSegments(mf, title, segment, ri, regions);
        }
        public async Task<Mediafile?> NoiseRemovalAsync(int id)
        {
            Mediafile? mf = GetFileSignedUrl(id);
            if (mf?.AudioUrl == null)
                return null;
            //TODO move this to a public place?
            string taskid = await Aeroservice.NoiseRemoval(mf.AudioUrl) ??throw new Exception("Noise Removal failed to start");
            await SaveTask(mf, "NR", taskid);
            return mf;
        }
        public async Task<Mediafile?> NoiseRemovalStatusAsync(int id, string taskId)
        {
            StatusInfo info = GetStatusInfo(id, "NR");

            //get a status...if done create a mediafile and return the new id
            string? result = await Aeroservice.NoiseRemovalStatus(taskId, info.s3File, info.folder );
            if (info.mediafile != null)
                info.mediafile.AudioQuality = result;
            return result == "PENDING" ? info.mediafile : result is null or "FAILURE" ? null : CreateNewMediafile(info);
        }

        public async Task<Mediafile?> VoiceConversion(int id, string targetUrl)
        {
            Mediafile? mf = GetFileSignedUrl(id);
            if (mf?.AudioUrl == null)
                return null;
            string taskId = await Aeroservice.VoiceConversion(mf.AudioUrl, targetUrl) ?? throw new Exception("Voice Conversion failed to start");
            await SaveTask(mf, "VC", taskId);
            return mf;
        }
        public async Task<Mediafile?> VoiceConversionStatus(int id, string taskId)
        {
            StatusInfo info = GetStatusInfo(id, "VC");

            //get a status...if done create a mediafile and return the new id
            string? result = await Aeroservice.VoiceConversionStatus(taskId, info.s3File, info.folder );
            if (info.mediafile != null)
                info.mediafile.AudioQuality = result;
            return result == "PENDING" ? info.mediafile : result is null or "FAILURE" ? null : CreateNewMediafile(info);
        }

        public async Task<Mediafile?> Transcription(int id, string iso, bool romanize)
        {
            Mediafile? mf = GetFileSignedUrl(id);
            if (mf?.AudioUrl == null)
                return null;
            bool testBatch = false;
            if (testBatch)
            {
                string[] filesurls = [mf.AudioUrl,mf.AudioUrl];
                string[] tasks = await Aeroservice.TranscriptionNew(filesurls, iso, romanize) ?? throw new Exception("Transcription failed to start");
                await SaveTasks([mf, mf], "TR", tasks);
            }
            else
            {
                //see if I have verse timing info
                List<Timing>? timing = GetVerseTiming(mf);
                float[]? times = null;
                if (timing != null)
                    times = timing.Select(t => t.start).ToArray();
                string[]? tasks = await Aeroservice.TranscriptionNew([mf.AudioUrl], iso, romanize, times) ?? throw new Exception("Transcription failed to start");
                if (tasks == null || tasks.Length == 0)
                    return mf;
                bool fakeIt = false;
                if (tasks.Length == 1)
                {
                    if (timing == null || !fakeIt)
                        await SaveTask(mf, "TR", tasks.First());
                    else
                    {
                        string[] otherTasks = ["84db7cd4-1ff6-4f1d-aa61-98fe82ab29e1", "6b73c406-e783-42d6-ac5a-e2b1bb3cd1b9", "993c1da0-344a-4079-bc48-43b878b945e4", "747214d4-38fb-4b4b-a2e7-1006d8ab5f88","84db7cd4-1ff6-4f1d-aa61-98fe82ab29e1", "6b73c406-e783-42d6-ac5a-e2b1bb3cd1b9", "993c1da0-344a-4079-bc48-43b878b945e4", "747214d4-38fb-4b4b-a2e7-1006d8ab5f88"];
                        List<string> fakeTasks = [tasks.First()];
                        for (int ix = 1; ix < timing.Count; ix++)
                        {
                            fakeTasks.Add(otherTasks[ix - 1]);
                        }
                        await SaveTasks(mf, "TR", timing, [.. fakeTasks]);
                    }
                }
                else
                {
                    if (timing != null)
                        await SaveTasks(mf, "TR", timing, tasks);
                    else
                        await SaveTask(mf, "TR", tasks.First());
                }
            }
            return mf;
        }
        public async Task<Mediafile?> TranscriptionStatus(int id, string taskId)
        {
            //get a status...if done create a mediafile and return the new id
            TranscriptionResponse? result = await Aeroservice.TranscriptionStatus(taskId);
            if (result != null)
            {
                Mediafile mf = MyRepository.Get(id) ?? throw new Exception("Mediafile not found");
                mf.Transcription = result.Transcription ?? result.Phonetic;
                return mf;
            }
            return null;
        }

        #endregion
    }
}
