using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    [Table(Tables.Integrations)]
    public partial class Integration : BaseModel, IArchive
    {
        public Integration() : base()
        {
            Name = "";
        }

        [Attr(PublicName = "name")]
        public string Name { get; set; }

        [Attr(PublicName = "url")]
        public string? Url { get; set; }

        public bool Archived { get; set; }
    }
}
