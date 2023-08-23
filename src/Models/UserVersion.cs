using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.UserVersions)]
    public class Userversion : Version
    {
        public string? Environment { get; set; }
    }
}
