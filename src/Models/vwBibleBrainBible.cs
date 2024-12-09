using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models;

public class Vwbiblebrainbible : Identifiable<int>
{
    [Attr(PublicName = "bibleid")]
    public string Bibleid { get; set; } = "";

    [Attr(PublicName = "bible-name")]
    public string BibleName { get; set; } = "";
    [Attr(PublicName = "pubdate")]
    public string Pubdate { get; set; } = "";

    [Attr(PublicName = "iso")]
    public string Iso { get; set; } = "";
    [Attr(PublicName = "language-name")]
    public string LanguageName { get; set; } = "";
    [Attr(PublicName = "nt-timing")]
    public bool NtTiming { get; set; }
    [Attr(PublicName = "ot")]
    public bool Ot { get; set; }
    [Attr(PublicName = "ot-timing")]
    public bool OtTiming { get; set; }
    [Attr(PublicName = "nt")]
    public bool Nt { get; set; }
}
