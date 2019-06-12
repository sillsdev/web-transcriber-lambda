using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Models
{
    interface ILastModified
    {
        int? LastModifiedBy { get; set; }
    }
}
