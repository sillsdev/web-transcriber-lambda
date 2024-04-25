using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models;

public class VWChecksum: Identifiable<int>
{
    [Attr(PublicName = "name")]
    public string Name { get; set; } = "";
    [Attr(PublicName = "project-id")]
    public int ProjectId { get; set; }

    [Attr(PublicName = "checksum")]
    public long Checksum { get; set; }
}
