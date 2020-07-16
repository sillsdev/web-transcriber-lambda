using System.Collections.Generic;
using Auth0.ManagementApi.Models;
using Newtonsoft.Json.Linq;
using SIL.Auth.Models;
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
        //List<SILAuth_Organization> SILOrganizations { get; }
        //int SilUserid { get; }
        UserSecret ParatextLogin(string connection, int userId);
        UserSecret ParatextToken(Identity ptIdentity, int userId);
        UserSecret ParatextToken(JToken ptIdentity, int id);
    }
}
