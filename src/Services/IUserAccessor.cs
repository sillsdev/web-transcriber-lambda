using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public interface IUserAccessor
    {
        bool IsAuthenticated { get; }
        //string UserId { get; }
        //string Role { get; }
        string Name { get; }
        string AuthId { get; }
    }
}

