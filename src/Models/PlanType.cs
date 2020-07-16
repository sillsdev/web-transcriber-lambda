using JsonApiDotNetCore.Models;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class PlanType : BaseModel
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("description")]
        public string Description { get; set; }

        [HasMany("Plans", Link.None)]
        public virtual List<Plan> Plans { get; set; }

    }
}
