﻿
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class ArtifactCategory : BaseModel, IArchive
    {
        [Attr("categoryname")]
        public string Categoryname { get; set; }
        [Attr("discussion")]
        public bool Discussion { get; set; }
        [Attr("resource")]
        public bool Resource { get; set; }
        public bool Archived { get; set; }
        public int? OrganizationId { get; set; }
        [HasOne("organization", Link.None)]
        public Organization Organization { get; set; }
    }
}