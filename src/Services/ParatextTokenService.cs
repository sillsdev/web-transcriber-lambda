using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Paratext.Models;
using SIL.Transcriber.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public class ParatextTokenService : EntityResourceService<ParatextToken>
    {
        public CurrentUserRepository CurrentUserRepository { get; }
        ParatextTokenRepository TokenRepository;

        public ParatextTokenService(
            IJsonApiContext jsonApiContext,
            ParatextTokenRepository tokenRepository,
            CurrentUserRepository currentUserRepository,
        ILoggerFactory loggerFactory)
    : base(jsonApiContext, tokenRepository, loggerFactory)
        {
            CurrentUserRepository = currentUserRepository;
            TokenRepository = tokenRepository;
        }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<IEnumerable<ParatextToken>> GetAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Models.User currentUser = CurrentUserRepository.GetCurrentUser();
            /* this fails since we havent' come from a controller
            ** var tokens = await base.GetAsync();
            */
            IQueryable<ParatextToken> tokens = TokenRepository.Get();
            return tokens.Where(t => t.UserId == currentUser.Id);
        }
    }
}
