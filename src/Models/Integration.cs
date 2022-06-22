using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    [Table("integrations")]
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

        [JsonIgnore]
        [HasMany(PublicName = "project-integrations")]
        public virtual List<Projectintegration>? ProjectIntegrations { get; set; }

        public bool Archived { get; set; }
    }
}
