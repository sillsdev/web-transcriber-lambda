using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table("userversions")]
    public class Userversion : Version
    {
        public string? Environment { get; set; }
    }
}
