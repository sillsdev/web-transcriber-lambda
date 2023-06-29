using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;
[Table(Tables.PassageNotes)]
public class Passagenote : BaseModel, IArchive
{
    public int? PassageId { get; set; }
    [HasOne(PublicName = "passage")]
    public Passage? Passage { get; set; }
    public int? NoteSectionId { get; set; }
    [HasOne(PublicName = "note-section")]
    public Section? NoteSection { get; set; }

    public bool Archived { get; set; }
}
