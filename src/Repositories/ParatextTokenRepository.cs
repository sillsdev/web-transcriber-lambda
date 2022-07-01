using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Paratext.Models;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class ParatextTokenRepository : AppDbContextRepository<ParatextToken>
    {
        private readonly CurrentUserRepository CurrentUserRepository;
        public ParatextTokenRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory,
                constraintProviders, loggerFactory, resourceDefinitionAccessor)
        {
            CurrentUserRepository = currentUserRepository;
        }
        protected override IQueryable<ParatextToken> GetAll()
        {
            Models.User? currentUser = CurrentUserRepository.GetCurrentUser();
            int id = currentUser?.Id ?? -1;
            return base.GetAll().Where(t => t.UserId == id);
        }
    }
}