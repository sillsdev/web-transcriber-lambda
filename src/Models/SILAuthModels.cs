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
        public string websiteurl { get; set; }
        public bool verified { get; set; }
        public string verifiedby { get; set; }
        public string verifieddate { get; set; }
        public bool active { get; set; }
        public int? userId { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }
    public class SILAuth_User: Auth0.ManagementApi.Models.User
    {
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
