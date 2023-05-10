using JsonApiDotNetCore.Resources.Annotations;
namespace SIL.Transcriber.Models;

public class Book : BaseModel
{
    [Attr(PublicName = "code")]
    public string Code { get; set; } = "";
}
