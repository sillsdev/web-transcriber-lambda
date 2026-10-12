using Auth0.ManagementApi;

namespace SIL.Transcriber.Services
{
    public interface IAuthService
    {
        Task<GetUserResponseContent> GetUserAsync(string Auth0Id);

        Task ResendVerification(string authId);
    }
}
