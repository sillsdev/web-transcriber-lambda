using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;

public class Biblebrainfileset : BaseModel
{
    [NotMapped]
    [Attr(PublicName = "allowed")]
    public string Allowed { get; set; } = ""; //allowed json

    [Attr(PublicName = "bible-id")]
    public string? BibleId { get; set; }
    [Attr(PublicName = "fileset-id")]
    public string FilesetId { get; set; } = ""; //id
    [Attr(PublicName = "media-type")]
    public string MediaType { get; set; } = ""; //type
    [Attr(PublicName = "fileset-size")]
    public string FilesetSize { get; set; } = ""; // NT, OT, ??
    [Attr(PublicName = "timing")]
    public bool Timing { get; set; }

    [Attr(PublicName = "codec")]
    public string? Codec { get; set; }  //mp3, opus
    [Attr(PublicName = "container")]
    public string? Container { get; set; } //mp3, webm,
    [Attr(PublicName = "licensor")]
    public string Licensor { get; set; } = ""; //licensor

}
