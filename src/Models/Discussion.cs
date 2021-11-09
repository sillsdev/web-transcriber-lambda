
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class Discussion : BaseModel, IArchive
    {
        [Attr("subject")]
        public string Subject { get; set; }
        public int Mediafileid { get; set; }
        [HasOne("mediafile")]
        public Mediafile Mediafile { get; set; }
        [Attr("segments")]
        [Column(TypeName = "jsonb")]
        public string Segments { get; set; }
        public bool Resolved { get; set; }
        public int Roleid { get; set; }
        [HasOne("role")]
        public Role Role { get; set; }
        public int Userid { get; set; }
        [HasOne("user")]
        public User User { get; set; }
        public bool Archived { get; set; }
    }
}
