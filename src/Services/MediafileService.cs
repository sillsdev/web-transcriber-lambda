using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using TranscriberAPI.Utility.Extensions;
using static SIL.Transcriber.Utility.ResourceHelpers;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static SIL.Transcriber.Utility.Extensions.ObjectExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SIL.Transcriber.Services
{
    class Tags
    {
        public string title = "";
        public string artist = "";
        public string album= "";
        public string cover="";
    }

    public class MediafileService : BaseArchiveService<Mediafile>
    {
        private IS3Service S3service { get; }
        private AeroService Aeroservice { get; }
        private PlanRepository PlanRepository { get; set; }
        private PassageRepository PassageRepository { get; set; }
        private MediafileRepository MyRepository { get; set; }
        private readonly AppDbContext dbContext;
        readonly private HttpContext? HttpContext;
        private readonly ICurrentUserContext CurrentUserContext;
        private readonly ILoggerFactory LoggerFactory;
        private readonly IHttpContextAccessor HttpContextAccessor;
        private class StatusInfo
        {
            public Mediafile? mediafile;
            public string s3File="";
            public string folder="";
        }
        public MediafileService(
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
        )
            : base(
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
            S3service = service;
            Aeroservice = aeroService;
            PlanRepository = planRepository;
            PassageRepository = passageRepository;
            MyRepository = myRepository;
            HttpContextAccessor = httpContextAccessor;
            HttpContext = httpContextAccessor.HttpContext;
            dbContext = (AppDbContext)contextResolver.GetContext();
            CurrentUserContext = currentUserContext;
            LoggerFactory = loggerFactory;
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
#pragma warning disable CS8602 // Dereference of a possibly null reference.
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
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            return null;
        }
        public IEnumerable<Mediafile>? WBTUpdate()
        {
            HttpContext?.SetFP("api");
            return MyRepository.WBTUpdate();
        }


        public string DirectoryName(Mediafile entity)
        {
            return entity.S3Folder ?? PlanRepository.DirectoryName(entity.Plan?.Id ?? entity.PlanId);
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
            return await S3service.GetFilename(DirectoryName(mf), mf.OriginalFile ?? "", (mf.S3Folder??"") != "" || mf.SourceMedia != null, suffix);
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
            if (!resource.ContentType?.StartsWith("text")??true)
            {
                resource.S3File = GetNewFileNameAsync(resource).Result;
                resource.AudioUrl = GetAudioUrl(resource);
            }
            return await base.CreateAsync(resource, cancellationToken);
        }
        public List<Mediafile> GetIPMedia(IQueryable<Organization> orgs)
        {
            IEnumerable<Intellectualproperty>? ip = dbContext.IntellectualPropertys.Join(orgs, ip => ip.OrganizationId, o => o.Id, (ip, o) => ip).ToList();
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
                if (ver != null) nextVers = ((ver.VersionNumber ?? 0) + 1).ToString();
            }

            Plan? plan = PlanRepository.GetWithProject(mf.PlanId);
            ret.mediafile = mf;
            ret.folder = mf.S3Folder ?? PlanRepository.DirectoryName(plan);
            ret.s3File =  $"{mf.S3File?[..(mf.S3File.Length - Path.GetExtension(mf.S3File)?.Length??0)]}{nextVers}{postfix}{Path.GetExtension(mf.S3File)}";
            return ret;
        }
        public async Task<Mediafile?> NoiseRemovalAsync(int id)
        {
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null)
                return null;
            S3Response response = await GetFile(id);
            //get a taskid from aero
            if (response?.FileStream != null)
            {
                string? taskid = await Aeroservice.NoiseRemoval(response.FileStream, mf.S3File??"");
                mf.TextQuality = taskid;
            }
            return mf;
        }
        public async Task<Mediafile?> NoiseRemovalStatusAsync(int id, string taskId)
        {
            StatusInfo info = GetStatusInfo(id, "nr");
            
            //get a status...if done create a mediafile and return the new id
            string? result = await Aeroservice.NoiseRemovalStatus(taskId, info.s3File, info.folder );
            if (info.mediafile != null) info.mediafile.AudioQuality = result;
            return result == "PENDING" ? info.mediafile : result is null or "FAILURE" ? null : CreateNewMediafile(info);
        }

        public async Task<string?> VoiceConversion(int id)
        {
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null)
                return null;
            S3Response response = await GetFile(id);
            //get a taskid from aero
            if (response?.FileStream != null)
            {
                return await Aeroservice.VoiceConversion(response.FileStream, mf.S3File??"");
            }
            return null;
        }
        public async Task<string?> VoiceConversionStatus(int id, string TaskId)
        {
            StatusInfo info = GetStatusInfo(id, "nr");

            //get a status...if done create a mediafile and return the new id
            return await Aeroservice.VoiceConversionStatus(TaskId);
        }
        public async Task<int> TranscriptionLanguages()
        {
            return await Aeroservice.TranscriptionLanguages();
        }
        public async Task<string?> Transcription(int id)
        {
            Mediafile? mf = MyRepository.Get(id);
            if (mf == null)
                return null;
            S3Response response = await GetFile(id);
            //get a taskid from aero
            if (response?.FileStream != null)
            {
                return await Aeroservice.Transcription(response.FileStream, mf.S3File??"");
            }
            return null;
        }
        public async Task<string> TranscriptionStatus(string TaskId)
        {
            return await Aeroservice.TranscriptionStatus(TaskId);
        }
        #endregion
    }
}
