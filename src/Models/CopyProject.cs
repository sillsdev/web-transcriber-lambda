namespace SIL.Transcriber.Models;

public class CopyProject : BaseModel
{
    public string Sourcetable { get; set; } = "";
    public string Newprojid { get; set; } = "";
    public string Oldid { get; set; } = "";
    public int Newid { get; set; }

}
