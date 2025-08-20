using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Response;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Repositories
{
    public partial class ProjDataRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        SectionService sectionService,
        SectionRepository sectionRepository,
        MediafileService mediaService,
        IMetaBuilder metaBuilder,
        IJsonApiOptions options
        ) : BaseRepository<Projdata>(
            targetedFields,
            contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor,
            currentUserRepository
            )
    {
        protected readonly SectionService SectionService = sectionService;
        protected readonly SectionRepository SectionRepository = sectionRepository;
        protected readonly MediafileService MediaService = mediaService;
        protected readonly ResourceInfo resourceInfo = new(resourceGraph, options, resourceDefinitionAccessor, metaBuilder);
        private IQueryable<Projdata> GetData(
            IQueryable<Projdata> entities,
            string project,
            string start,
            int version = 1
        )
        {
            string data = "";
            if (!int.TryParse(start, out int iStart))
                iStart = 0;
            int iStartNext = iStart;
            if (!int.TryParse(project, out int projectid))
                projectid = 0;
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            string snapshotDate = DateTime.UtcNow.ToString();
            do
            {
                //plans
                IEnumerable<Plan> plans = dbContext.PlansData.Where(
                    x => x.ProjectId == projectid && !x.Archived
                );
                //plans moved to orgdata
                //if (!CheckAdd(0, plans, dtBail, Serializer, ref iStartNext, ref data)) break;

                //sections
                IQueryable<Section> sections = dbContext.SectionsData
                    .Join(plans, s => s.PlanId, pl => pl.Id, (s, pl) => s)
                    .Where(x => !x.Archived);

                //passages
                IQueryable<Passage> passages = dbContext.PassagesData
                    .Join(sections, p => p.SectionId, s => s.Id, (p, s) => p)
                    .Where(x => !x.Archived);
                IQueryable<Sharedresource> linkedresources = passages.Join(dbContext.SharedresourcesData,
                    p => p.SharedResourceId, r => r.Id, (p, r) => r);
                IQueryable<Passage> linkedpassages = linkedresources
                    .Join(dbContext.PassagesData, r => r.PassageId, p => p.Id, (r, p) => p)
                    .Where(x => !x.Archived);
                IQueryable<Section> linkedsections = linkedpassages
                    .Join(dbContext.SectionsData, p => p.SectionId, s => s.Id, (p, s) => s)
                    .Where(x => !x.Archived);

                IEnumerable<Section> sectionsWithLinked = sections.ToList().Union(linkedsections);
                IQueryable<Section> sects = dbContext.SectionsData.Where(s => sectionsWithLinked.Select(s2 => s2.Id).Contains(s.Id)).OrderBy(s => s.Id);
                if (!CheckAddData(0, sects, dtBail, ref iStartNext, ref data, resourceInfo))
                    break;
                IEnumerable<Passage> passagesWithLinked = passages.ToList().Union(linkedpassages);
                IQueryable<Passage> psgs = dbContext.PassagesData.Where(p => passagesWithLinked.Select(s2 => s2.Id).Contains(p.Id)).OrderBy(p => p.Id);

                if (!CheckAddData(1, psgs, dtBail, ref iStartNext, ref data, resourceInfo))
                    break;

                List<Mediafile>linkedmediafiles = [];
                linkedpassages.ToList().ForEach(p => {
                    Mediafile? m = MediaService.GetLatest(p.Id);
                    if (m != null)
                        linkedmediafiles.Add(m);
                });
                IQueryable<Mediafile> media = dbContext.MediafilesData
                   .Where(m => linkedmediafiles.Select(lm => lm.Id).Contains(m.Id)).OrderBy(m => m.Id);
                if (!CheckAddData(2, media, dtBail, ref iStartNext, ref data, resourceInfo))
                    break;

                //mediafiles
                IQueryable<Mediafile>? mediafiles = dbContext.MediafilesData
                    .Join(plans, m => m.PlanId, pl => pl.Id, (m, pl) => m)
                    .Where(x => !x.Archived).OrderBy(m=>m.Id);
                if (!CheckAddData(3, mediafiles, dtBail, ref iStartNext, ref data, resourceInfo))
                    break;

                //passagestatechanges
                if (!CheckAddData(4, dbContext.PassagestatechangesData.Join(
                                passages,
                                psc => psc.PassageId,
                                p => p.Id,
                                (psc, p) => psc
                            ).OrderBy(m => m.Id), dtBail, ref iStartNext, ref data, resourceInfo))
                    break;


                if (version > 3)
                {
                    //discussions
                    IQueryable<Discussion> discussions = dbContext.DiscussionsData
                        .Join(mediafiles, d => d.MediafileId, m => m.Id, (d, m) => d)
                        .Where(x => !x.Archived).OrderBy(d => d.Id);
                    if (
                        !CheckAddData(5,
                            discussions,
                            dtBail,
                            ref iStartNext,
                            ref data, resourceInfo
                        )
                    )
                        break;

                    //comments
                    if (
                        !CheckAddData(6,
                                dbContext.CommentsData
                                    .Join(discussions, c => c.DiscussionId, d => d.Id, (c, d) => c)
                                    .Where(x => !x.Archived).OrderBy(d => d.Id),
                            dtBail,
                            ref iStartNext,
                            ref data, resourceInfo
                        )
                    )
                        break;

                    IQueryable<Sectionresource> sectionresources = dbContext.SectionresourcesData
                        .Join(sections, sr => sr.SectionId, s => s.Id, (sr, s) => sr)
                        .Where(x => !x.Archived).OrderBy(x => x.Id);
                    if (
                        !CheckAddData(7,
                            sectionresources,
                            dtBail,
                            ref iStartNext,
                            ref data, resourceInfo
                        )
                    )
                        break;

                    IQueryable<Sectionresourceuser> srusers = dbContext.SectionresourceusersData
                        .Join(sectionresources, u => u.SectionResourceId, sr => sr.Id, (u, sr) => u)
                        .Where(x => !x.Archived).OrderBy(x => x.Id);
                    if (
                        !CheckAddData(8,
                            srusers,
                            dtBail,
                            ref iStartNext,
                            ref data, resourceInfo
                        )
                    )
                        break;
                }
                iStartNext = -1; //Done!
            } while (false); //do it once
            if (iStart == iStartNext)
                throw new System.Exception("Single table is too large to return data" + iStart.ToString());

            Projdata ProjData = entities.First();
            ProjData.Json = data + FinishData();
            ProjData.StartNext = iStartNext;
            ProjData.SnapshotDate = snapshotDate;
            return entities;
        }

        protected override IQueryable<Projdata> FromStartIndex(
            IQueryable<Projdata>? entities,
            string startIndex,
            string version = "",
            string projectid = ""
        )
        {
            dynamic? x = JsonConvert.DeserializeObject(version);
            int filterVersion = x?.version ?? 1;
            return GetData(entities ?? GetAll(), projectid, startIndex, filterVersion);
        }

        public override IQueryable<Projdata> FromProjectList(
            IQueryable<Projdata>? entities,
            string idList
        )
        {
            IQueryable<Projdata> result = GetData(entities ?? GetAll(), idList, "0");
            return result;
        }

        protected override IQueryable<Projdata> GetAll()
        {
            List<Projdata> entities = [new Projdata()];
            return entities.AsAsyncQueryable();
        }

        public override IQueryable<Projdata> FromCurrentUser(IQueryable<Projdata>? entities = null)
        {
            return GetAll();
        }

    }
}
