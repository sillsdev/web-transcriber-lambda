using JsonApiDotNetCore.Models;
using Microsoft.IdentityModel.Tokens;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;


namespace SIL.Paratext.Models
{
    public class ParatextToken : Identifiable<int>
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public int UserId { get; set; }
        [NotMapped]
        public string OriginalRefreshToken { get; set; }

        [NotMapped]
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
        [NotMapped]
        public DateTime ValidTo
        {
            get
            {
                var accessToken = new JwtSecurityToken(AccessToken);
                return accessToken.ValidTo;
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
