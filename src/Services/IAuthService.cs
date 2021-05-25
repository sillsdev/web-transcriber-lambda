using SIL.Auth.Models;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public interface IAuthService
    {
        bool ValidateWebhookCredentials(string username, string password);
        SILAuth_User GetUser(string Auth0Id);
        //Task LinkAccountAsync(string primaryAuthId, string secondaryAuthId);
        Task ResendVerification(string authId);
    }
}
