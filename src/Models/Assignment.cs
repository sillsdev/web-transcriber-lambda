using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace SIL.Transcriber.Models
{
    public class Assignment
    {
        public User User { get; set; } = null!;
        public Role Role { get; set; } = null!;
    }
}
