namespace SIL.Transcriber.Models
{
    public interface ILastModified
    {
        int? LastModifiedBy { get; set; }
        User? LastModifiedByUser { get; set; }
        string LastModifiedOrigin { get; set; }
    }
}
