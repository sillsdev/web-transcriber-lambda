using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;


namespace SIL.Transcriber.Utility
{
    public static class HttpContextHelpers
    {
        public static async Task<string> GetJWT(this HttpContext context)
        {
            string scheme = JwtBearerDefaults.AuthenticationScheme;
            string token = await context.GetTokenAsync(scheme, "access_token") ?? "";

            return token;
        }
        public static string? GetOrigin(this HttpContext context)
        {
            return context.Request.Headers.FirstOrDefault(h => h.Key.Equals("origin", StringComparison.CurrentCultureIgnoreCase)).Value;
        }

        public static string? GetFP(this HttpContext context)
        {
            return context.Request.Headers.FirstOrDefault(h => h.Key.Equals("x-fp", StringComparison.CurrentCultureIgnoreCase)).Value;
        }
        public static void SetFP(this HttpContext context, string value)
        {
            context.Request.Headers["x-fp"] = value;
        }

        public const string TYPE_NAME_EMAIL = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

        // NOTE: User Claims of Interest:
        //   - type of name => email the user signed up with
        //   - type of http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress => email address
        //     ( this is the reliable way to get the email )
        //   - type of http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier => auth0Id
        //   - type of exp => expiration date in seconds since the epoch
        //   - type of access_token => the full JWT
        public const string TYPE_NAME_IDENTIFIER = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
        public static string GetAuth0Id(this HttpContext context)
        {
            return context
                .User.Claims
                .FirstOrDefault(c => c.Type == TYPE_NAME_IDENTIFIER)
                ?.Value ?? "";
        }

        public static string GetAuth0Type(this HttpContext context)
        {
            return context
                .User.Claims
                .FirstOrDefault(c => c.Type == TYPE_NAME_IDENTIFIER)
                ?.Value ?? "Bearer";
        }
        public static string GetAuth0Email(this HttpContext context)
        {
            return context
                .User.Claims
                .FirstOrDefault(c => c.Type == TYPE_NAME_EMAIL)  //should be there but isnt...
                ?.Value ?? "";
        }

        public const string TYPE_NAME_GIVEN_NAME = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname";
        public static string GetAuth0GivenName(this HttpContext context)
        {
            return context
                .User.Claims
                .FirstOrDefault(c => c.Type == TYPE_NAME_GIVEN_NAME)
                ?.Value ?? "";
        }

        public const string TYPE_NAME_SUR_NAME = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname";
        public static string GetAuth0SurName(this HttpContext context)
        {
            return context
                .User.Claims
                .FirstOrDefault(c => c.Type == TYPE_NAME_SUR_NAME)
                ?.Value ?? "";
        }

        public const string TYPE_NAME_NAME = "name";
        public static string GetAuth0Name(this HttpContext context)
        {
            return context
                .User.Claims
                .FirstOrDefault(c => c.Type == TYPE_NAME_NAME)
                ?.Value ?? "";
        }
    }
}
