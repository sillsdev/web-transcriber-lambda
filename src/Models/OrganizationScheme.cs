using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;
[Table(Tables.OrganizationSchemes)]

public partial class Organizationscheme : BaseModel, IArchive
{
    [Attr(PublicName = "name")]
    public string Name { get; set; } = "";

    [HasOne(PublicName = "organization")]
    public virtual Organization Organization { get; set; } = null!;

    public int OrganizationId { get; set; }

    public bool Archived { get; set; }
}



