using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Utility.Extensions;

namespace SIL.Logging.Models
{
    public class ParatextTokenHistory : LogBaseModel
    {
        public ParatextTokenHistory() : base() {
            AccessToken = "";
            RefreshToken = "";
            Msg = "";
        }
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

        [Attr(PublicName="access-token")]
        public string AccessToken { get; set; }
        [Attr(PublicName="refresh-token")]
        public string RefreshToken { get; set; }

        [Attr(PublicName="msg")]
        public string Msg { get; set; }
        [Attr(PublicName="err-msg")]
        public string? ErrMsg { get; set; }
        private DateTime _issued;
        [Attr(PublicName = "issued-at")]
        public DateTime IssuedAt { get { return _issued; } set { _issued = value.SetKindUtc(); } }

        private DateTime _validto;
        [Attr(PublicName="valid-to")]
        public DateTime ValidTo  { get { return _validto; } set { _validto = value.SetKindUtc(); } }


    }
}
