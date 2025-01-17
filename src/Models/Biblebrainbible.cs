using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models;

public class Biblebrainbible : BaseModel
{
    [Attr(PublicName = "iso")]
    public string Iso { get; set; } = "";
    [Attr(PublicName = "languagename")]
    public string LanguageName { get; set; } = "";//language
    [Attr(PublicName = "languageid")]
    public int LanguageId { get; set; }
    [Attr(PublicName = "biblename")]
    public string BibleName { get; set; } = ""; //name
    [Attr(PublicName = "shortname")]
    public string ShortName { get; set; } = ""; //vname
    [Attr(PublicName = "bibleid")]
    public string BibleId { get; set; } = "";// abbr
    [Attr(PublicName = "pubdate")]
    public string Pubdate { get; set; } = "";// date
    public string? Copyright { get; set; } = "";
}
