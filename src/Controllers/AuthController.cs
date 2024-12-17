using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AuthController(ICurrentUserContext currentUserContext, IAuthService authService) : ControllerBase()
    {
        protected IAuthService service = authService;
        protected ICurrentUserContext currentUserContext = currentUserContext;

        [HttpGet("resend")]
        public Task Resend()
        {
            return service.ResendVerification(currentUserContext.Auth0Id);
        }
    }
}
