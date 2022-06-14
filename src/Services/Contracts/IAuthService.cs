using Auth0.ManagementApi.Models;

namespace SIL.Transcriber.Services
{
    public interface IAuthService
    {
        Task<User> GetUserAsync(string Auth0Id);

        Task ResendVerification(string authId);
    }
}
