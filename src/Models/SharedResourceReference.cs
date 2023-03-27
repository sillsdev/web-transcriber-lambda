using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;

public class Sharedresourcereference : BaseModel, IArchive
{
    public int SharedResourceId { get; set; }
    [HasOne(PublicName = "shared-resource")]
    public Sharedresource? SharedResource { get; set; }

    [Attr(PublicName = "book")]
    public string Book { get; set; } = "";

    [Attr(PublicName = "chapter")]
    public int Chapter { get; set; }
    [Attr(PublicName = "verses")]
    public string? Verses { get; set; }

    public bool Archived { get; set; }

}