using JsonApiDotNetCore.Resources.Annotations;
namespace SIL.Transcriber.Models;

public class Orgkeytermreference : BaseModel, IArchive
{
    public int OrgkeytermId { get; set; }
    [HasOne(PublicName = "orgkeyterm")]
    public Orgkeyterm? Orgkeyterm { get; set; }

    public int ProjectId { get; set; }
    [HasOne(PublicName = "project")]
    public Project? Project { get; set; }

    public int SectionId { get; set; }
    [HasOne(PublicName = "section")]
    public Section? Section { get; set; }

    public bool Archived { get; set; }

    [Attr(PublicName = "offline-id")]
    public string? OfflineId { get; set; }
}