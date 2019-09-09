using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Auth.Models
{
    public class SILAuth_Membership
    {
        public int id { get; set; }
        public int? userId { get; set; }
        public int? orgId { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }
    public class SILAuth_Organization
    {
        public int id { get; set; }
        public int guid { get; set; }
        public string uniquekey { get; set; }
        public int code { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string logo { get; set; }
        public bool verified { get; set; }
        public string verifiedby { get; set; }
        public string verifieddate { get; set; }
        public bool active { get; set; }
        public int? userId { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }
    public class SILAuth_User
    {
        public int id { get; set; }
        public string name { get; set; }
        public string givenname { get; set; }
        public string familyname { get; set; }
        public string nickname { get; set; }
        public string email { get; set; }
        public bool emailverified { get; set; }
        public string phone { get; set; }
        public string timezone { get; set; }
        public string locale { get; set; }
        public bool? isLocked { get; set; }
        public string externalid { get; set; }
        public string profilevisibility { get; set; }
        public bool? emailnotification { get; set; }
        public string identitytoken { get; set; }
        public string uilanguagebcp47 { get; set; }
        public string avatarurl { get; set; }
        public bool? archived { get; set; }
        public string authid { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }
    public class SILAuth_Invite
    {
        public int id { get; set; }
        public string email { get; set; }
        public int orgId { get; set; }
        public int userId { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }

}
