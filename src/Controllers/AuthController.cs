using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AuthController : ControllerBase
    {
        protected IAuthService service;
        protected ICurrentUserContext currentUserContext;

        public AuthController(
            ICurrentUserContext currentUserContext,
            IAuthService authService)
           : base()
        {
            this.service = authService;
            this.currentUserContext = currentUserContext;
        }

        [HttpGet("resend")]
        public Task Resend()
        {
            return service.ResendVerification(currentUserContext.Auth0Id);
        }

    }
}
