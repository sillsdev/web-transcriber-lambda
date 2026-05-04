using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
namespace SIL.Transcriber.Models;

[Resource(GenerateControllerEndpoints = JsonApiEndpoints.None)]
public class SimpleResponse : Identifiable<int>
{
    [Attr(PublicName = "message")]
    public string Message { get; set; } = "";
}