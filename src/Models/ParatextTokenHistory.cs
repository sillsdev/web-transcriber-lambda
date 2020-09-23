using JsonApiDotNetCore.Models;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;


namespace SIL.Logging.Models
{
    public class ParatextTokenHistory : LogBaseModel
    {
        public ParatextTokenHistory() : base() { }
        public ParatextTokenHistory(int userid, string access, string refresh, string msg, string errmsg="") : base(userid) {
            AccessToken = access;
            RefreshToken = refresh;
            Msg = msg;
            ErrMsg = errmsg;
            if (access != null)
            {
                JwtSecurityToken jwt = new JwtSecurityToken(access);
                IssuedAt = jwt.Payload.Iat != null ? EpochTime.DateTime((long)jwt.Payload.Iat) : DateTime.MinValue;
                ValidTo = jwt.ValidTo;
                Console.WriteLine(IssuedAt.ToString(), ValidTo.ToString());
            }
        }

        [Attr("access-token")]
        public string AccessToken { get; set; }
        [Attr("refresh-token")]
        public string RefreshToken { get; set; }

        [Attr("msg")]
        public string Msg { get; set; }
        [Attr("err-msg")]
        public string ErrMsg { get; set; }
        [Attr("issued-at")]
        public DateTime IssuedAt { get; set; }

        [Attr("valid-to")]
        public DateTime ValidTo { get; set; }

    }
}
