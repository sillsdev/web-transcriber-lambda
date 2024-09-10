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

namespace SIL.Transcriber.Services
{
    public class MediafileService : BaseArchiveService<Mediafile>
    {
        private IS3Service S3service { get; }
        private PlanRepository PlanRepository { get; set; }
        private PassageRepository PassageRepository { get; set; }
        private MediafileRepository MyRepository { get; set; }
        private readonly AppDbContext dbContext;
        readonly private HttpContext? HttpContext;
        private readonly ICurrentUserContext CurrentUserContext;
        private readonly ILoggerFactory LoggerFactory;
        private readonly IHttpContextAccessor HttpContextAccessor;
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
            return files.SingleOrDefault(p => p.S3File == s3File && p.PlanId == plan);
        }
        public IEnumerable<Mediafile>? WBTUpdate()
        {
            HttpContext?.SetFP("api");
            return MyRepository.WBTUpdate();
        }


        public string DirectoryName(Mediafile entity)
        {
            return PlanRepository.DirectoryName(entity.Plan?.Id ?? entity.PlanId);
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
            return await S3service.GetFilename(DirectoryName(mf), mf.OriginalFile??"", mf.SourceMedia != null, suffix);
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
            if (
                mf.S3File.Length == 0
                || !(await S3service.FileExistsAsync(mf.S3File, PlanRepository.DirectoryName(plan)))
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
        private async Task<Mediafile> Reload(Mediafile m)
        {
            using AppDbContext context = GetMyOwnContext();
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
        private static string PadSeqNum(decimal? seq)
        {
            if (seq == null) return "";
            string[]? parts = seq?.ToString().Split(".");
            return parts?[0].PadLeft(3, '0') + (parts?.Length > 1 ? "." + parts [1] : "");
        }
        private string PublishFilename(Mediafile m)
        {
            Passage? p = dbContext.PassagesData.SingleOrDefault(p => p.Id == (m.PassageId ?? 0));
            string bibleId = PlanRepository.BibleId(m.PlanId);
            if (bibleId == "") throw new Exception("No BibleId found");
            string fileName = m.PublishedAs??"";
            if (fileName == "")
            {
                string book = p?.Book ?? "";
                fileName = $"{book}{PadSeqNum(p?.Section?.Sequencenum)}{PadSeqNum(p?.Sequencenum)}";
                if (fileName.Length > 0)
                    fileName += "_";
                if (p?.StartChapter != null)
                {
                    string startChap = p.StartChapter?.ToString().PadLeft(3, '0') ?? "";
                    string endChap = p.EndChapter?.ToString().PadLeft(3, '0') ?? "";
                    string startVerse = p.StartVerse?.ToString().PadLeft(3, '0') ?? "";
                    string endVerse = (p.EndVerse ?? p.StartVerse)?.ToString().PadLeft(3, '0') ?? "";
                    fileName = startChap == endChap ?
                        startVerse == endVerse ?
                            $"{fileName}_c{startChap}_{startVerse}" :
                            $"{fileName}_c{startChap}_{startVerse}-{endVerse}"
                        : $"{fileName}_c{startChap}_{startVerse}-c{endChap}_{endVerse}";
                }
                else if (p?.Passagetype?.Abbrev == "NOTE")
                {
                    Sharedresource? sr = dbContext.SharedresourcesData.SingleOrDefault(sr => sr.Id == p.SharedResourceId);
                    sr ??= dbContext.SharedresourcesData.SingleOrDefault(sr => sr.PassageId == p.Id);
                    fileName = (sr?.Title ?? "") != ""
                        ? $"{fileName}NOTE_{FileName.CleanFileName(sr?.Title ?? "")}"
                        : $"{fileName}{Path.ChangeExtension(m.OriginalFile, ".mp3")}";
                }
                else if (p?.Passagetype?.Abbrev == "CHNUM")
                {
                    fileName = $"{fileName}{FileName.CleanFileName(p.Reference??Path.ChangeExtension(m.OriginalFile, ".mp3")??p.Id.ToString())}";
                }
                else
                {
                    Section? s = dbContext.SectionsData.SingleOrDefault(s => s.TitleMediafileId == m.Id);
                    if (s != null)
                    {
                        fileName = $"{fileName}{FileName.CleanFileName(s.Plan?.Name??"")}{PadSeqNum(s.Sequencenum)}{FileName.CleanFileName(s.Name)}";
                    }
                    else if (m.OriginalFile != null)
                        fileName = $"{fileName}{FileName.CleanFileName(Path.ChangeExtension(m.OriginalFile, ".mp3"))}";
                }
            }
            if (!fileName.StartsWith(bibleId))
            {                 
                fileName = $"{bibleId}_{fileName}";
            } 
            if (!fileName.EndsWith(".mp3"))
            {
                fileName = $"{fileName}.mp3";
            }
            return fileName;
        }

        public async Task<string> Publish(int id, string publishTo)
        {
            Mediafile? m = MyRepository.Get(id);
            if (m == null)
            {
                return "";
            }
            if (m.S3File == null)
            {
                m = await Reload(m);
            }
            Plan? plan = PlanRepository.GetWithProject(m.PlanId);
            string outputKey = PublishFilename(m);
            string inputKey = $"{PlanRepository.DirectoryName(plan)}/{m.S3File ?? ""}";
            S3Response response = await S3service.CreatePublishRequest(m.Id, inputKey, outputKey);
            if ( response.Status == HttpStatusCode.OK)
            {
                using AppDbContext context = GetMyOwnContext();
                m.PublishedAs = outputKey;
                m.ReadyToShare = true;
                m.PublishTo = publishTo;
                context.Mediafiles.Update(m);
                context.SaveChanges(); 
            }
            return response.Message;
        }
        public Mediafile? GetLatest(int passageId)
        {
            return dbContext.MediafilesData
                .Where(mf => mf.PassageId == passageId)
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
            resource.S3File = GetNewFileNameAsync(resource).Result;
            resource.AudioUrl = GetAudioUrl(resource);
            return await base.CreateAsync(resource, cancellationToken);
        }
        public List<Mediafile> GetIPMedia(IQueryable<Organization> orgs)
        {
            IEnumerable<Intellectualproperty>? ip = dbContext.IntellectualPropertys.Join(orgs, ip => ip.OrganizationId, o => o.Id, (ip, o) => ip).ToList();
            return ip.Join(dbContext.Mediafiles, ip => ip.ReleaseMediafileId, m => m.Id, (ip, m) => m).ToList();
        }

    }
}
