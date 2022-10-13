
using Auth0.ManagementApi.Models;
using Newtonsoft.Json.Linq;
using SIL.Paratext.Models;

namespace SIL.Transcriber.Services
{
    public interface ICurrentUserContext
    {
        string Auth0Id { get; }
        string Email { get; }
        string GivenName { get; }
        string FamilyName { get; }
        string Name { get; }
        string Avatar { get; }
        bool EmailVerified { get; }
        UserSecret? ParatextLogin(string connection, int userId);
        UserSecret? ParatextToken(Identity ptIdentity, int userId);
        UserSecret? ParatextToken(JToken ptIdentity, int id);
    }
}

