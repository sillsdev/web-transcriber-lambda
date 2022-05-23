using Auth0.ManagementApi.Models;

namespace SIL.Transcriber.Services
{
    public interface IAuthService
    {
        bool ValidateWebhookCredentials(string username, string password);
        Task<User> GetUserAsync(string Auth0Id);
        //Task LinkAccountAsync(string primaryAuthId, string secondaryAuthId);
        Task ResendVerification(string authId);
    }
}
