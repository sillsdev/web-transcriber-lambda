using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Models
{
    public interface IArchive
    {
        bool Archived { get; set; }
    }
}
