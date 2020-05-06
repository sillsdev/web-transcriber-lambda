using JsonApiDotNetCore.Models;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Integration : BaseModel, IArchive
    {
        public enum Integrations
        {
            Paratext = 1
        }

        [Attr("name")]
        public string Name { get; set; }

        [Attr("url")]
        public string Url { get; set; }

        [HasMany("project-integrations", Link.None)]
        public virtual List<ProjectIntegration> ProjectIntegrations { get; set; }

        //public ICollection<ProjectIntegration> Projectintegrations { get; set; }
        public bool Archived { get; set; }
    }
}
