using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace SIL.Transcriber.Models;

[Table("orgkeyterms")]

public class Orgkeyterm : BaseModel, IArchive
{
    [Attr(PublicName = "organization-id")]
    public int OrganizationId { get; set; }
    [HasOne(PublicName = "organization")]
    public Organization? Organization { get; set; }
    public string Term { get; set; } = "";
    public string Domain { get; set; } = "";
    public string? Definition { get; set; }
    public string Category { get; set; } = "";
    public bool Archived { get; set; }
    [Attr(PublicName = "offline-id")]
    public string? OfflineId { get; set; }
}

