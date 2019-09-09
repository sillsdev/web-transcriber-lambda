using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Models
{
    public class Tokens
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }

        public DateTime IssuedAt
        {
            get
            {
                var accessToken = new JwtSecurityToken(AccessToken);
                if (accessToken.Payload.Iat != null)
                    return EpochTime.DateTime((long)accessToken.Payload.Iat);
                return DateTime.MinValue;
            }
        }

        public bool ValidateLifetime()
        {
            var accessToken = new JwtSecurityToken(AccessToken);
            var now = DateTime.UtcNow;
            return now >= accessToken.ValidFrom && now <= accessToken.ValidTo;
        }
    }
}
