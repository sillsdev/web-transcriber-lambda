using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SIL.Transcriber.Models
{
    public class S3Response 
    {
        public HttpStatusCode Status { get; set; }
        public string Message { get; set; }
        public Stream FileStream { get; set; }
        public string ContentType { get; set; }
    }
}
