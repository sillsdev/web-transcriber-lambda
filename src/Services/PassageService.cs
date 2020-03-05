using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class PassageService : BaseArchiveService<Passage>
    {

        public PassageService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<Passage> PassageRepository,
          ILoggerFactory loggerFactory) : base(jsonApiContext, PassageRepository, loggerFactory)
        {
        }

        public override async Task<IEnumerable<Passage>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);
        }

        public override async Task<Passage> GetAsync(int id)
        {
            var passages = await GetAsync();

            return passages.SingleOrDefault(g => g.Id == id);
        }
        
        public IQueryable<Passage> GetBySection(int SectionId)
        {
            PassageRepository pr = (PassageRepository)MyRepository;
            return pr.Get()
                    .Include(p => p.Section)
                    .Include(p=> p.Mediafiles);
            
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
            return mediafile != null ? mediafile.Transcription ?? "" : "";
        }
    }
}