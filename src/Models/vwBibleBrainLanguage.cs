using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models;

public class Vwbiblebrainlanguage : Identifiable<int>
{
    [Attr(PublicName = "iso")]
    public string Iso { get; set; } = "";
    [Attr(PublicName = "language-name")]
    public string LanguageName { get; set; } = "";
    [Attr(PublicName = "timing")]
    public bool Timing { get; set; }
    [Attr(PublicName = "ot")]
    public bool Ot { get; set; }
    [Attr(PublicName = "nt")]
    public bool Nt { get; set; }
}
