using JsonApiDotNetCore.Resources.Annotations;
namespace SIL.Transcriber.Models;

public class Orgkeytermreference : BaseModel, IArchive
{
    [Attr(PublicName = "orgkeyterm-id")]
    public int OrgkeytermId { get; set; }
    [HasOne(PublicName = "orgkeyterm")]
    public Orgkeyterm? Orgkeyterm { get; set; }

    [Attr(PublicName = "project-id")]
    public int ProjectId { get; set; }
    [HasOne(PublicName = "project")]
    public Project? Project { get; set; }

    [Attr(PublicName = "section-id")]
    public int SectionId { get; set; }
    [HasOne(PublicName = "section")]
    public Section? Section { get; set; }

    public bool Archived { get; set; }

}