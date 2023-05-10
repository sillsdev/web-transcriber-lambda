using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;

public class CopyProject : BaseModel
{
    public string Sourcetable { get; set; } = "";
    public int Newprojid { get; set; }
    public int Oldid { get; set; }
    public int Newid { get; set; }
    
}
