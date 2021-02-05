using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class PassageService : BaseArchiveService<Passage>
    {
        private HttpContext HttpContext;

        public PassageService(
            IJsonApiContext jsonApiContext,
            PassageRepository PassageRepository,
            IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory) : base(jsonApiContext, PassageRepository, loggerFactory)
        {
            HttpContext = httpContextAccessor.HttpContext;
        }

        public override async Task<IEnumerable<Passage>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);
        }

        public override async Task<Passage> GetAsync(int id)
        {
            IEnumerable<Passage> passages = await GetAsync();

            return passages.SingleOrDefault(g => g.Id == id);
        }

        public IQueryable<Passage> GetBySection(int SectionId)
        {
            return MyRepository.Get()
                    .Include(p => p.Section)
                    .Include(p=> p.Mediafiles)
                    .Where(p => p.SectionId == SectionId);
        }
        public IQueryable<Passage> Get(int id)
        {
            return MyRepository.Get().Include(p => p.Section).Where(p => p.Id == id);
        }
        public int GetProjectId(Passage passage)
        {
            PassageRepository pr = (PassageRepository)MyRepository;
            return pr.ProjectId(passage);
        }
        public IQueryable<Passage> ReadyToSync(int PlanId)
        {
            PassageRepository pr = (PassageRepository)MyRepository;
            return pr.ReadyToSync(PlanId);
        }
        public string GetTranscription(Passage passage)
        {
            PassageRepository pr = (PassageRepository)MyRepository;
            if (passage.Mediafiles == null)
            {
                passage = pr.Get().Where(p => p.Id == passage.Id).Include(p => p.Mediafiles).FirstOrDefault();
            }
            Mediafile mediafile = passage.Mediafiles.OrderByDescending(mf => mf.VersionNumber).FirstOrDefault();
            string trans = mediafile != null ? mediafile.Transcription ?? "" : "";
            //remove timestamp
            string pattern = @"\([0-9]{1,2}:[0-9]{2}(:[0-9]{2})?\)";
            return Regex.Replace(trans,pattern, "");
           
        }
        public async Task<Passage> UpdateToReadyStateAsync(int id)
        {
            PassageRepository pr = (PassageRepository)MyRepository;

            Passage p = await pr.GetAsync(id);
            p.State = "transcribeReady";
            string fp = HttpContext.GetFP();
            HttpContext.SetFP("api");  //even the guy who sent this needs these changes
            await base.UpdateAsync(id, p);
            HttpContext.SetFP(fp);
            return p;
        }
    }
}