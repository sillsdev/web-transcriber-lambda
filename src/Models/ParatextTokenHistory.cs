using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Utility.Extensions;
using System.IdentityModel.Tokens.Jwt;

namespace SIL.Logging.Models
{
    public class Paratexttokenhistory : LogBaseModel
    {
        public Paratexttokenhistory() : base()
        {
            AccessToken = "";
            RefreshToken = "";
            Msg = "";
            UserId = 0;
        }
        public Paratexttokenhistory(int userid, string access, string refresh, string msg, string errmsg = "") : base(userid)
        {
            AccessToken = access;
            RefreshToken = refresh;
            Msg = msg;
            ErrMsg = errmsg;
            if (access != null)
            {
                JwtSecurityToken jwt = new (access);
                IssuedAt = jwt.IssuedAt;
                ValidTo = jwt.ValidTo;
                Console.WriteLine(IssuedAt.ToString(), ValidTo.ToString());
            }
        }

        [Attr(PublicName = "access-token")]
        public string AccessToken { get; set; } = "";

        [Attr(PublicName = "refresh-token")]
        public string RefreshToken { get; set; } = "";

        [Attr(PublicName = "msg")]
        public string Msg { get; set; } = "";
        [Attr(PublicName = "err-msg")]
        public string? ErrMsg { get; set; }
        private DateTime _issued;
        [Attr(PublicName = "issued-at")]
        public DateTime IssuedAt { get { return _issued; } set { _issued = value.SetKindUtc(); } }

        private DateTime _validto;
        [Attr(PublicName = "valid-to")]
        public DateTime ValidTo { get { return _validto; } set { _validto = value.SetKindUtc(); } }


    }
}
