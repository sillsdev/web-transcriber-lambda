using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Logging.Models
{
    public partial class Paratextsync : LogBaseModel
    {
        public Paratextsync() : base()
        {
        }
        public Paratextsync(int userid, int planid, string paratextproject, string bookchapter, string beforesync) : base(userid)
        {
            PlanId = planid;
            ParatextProject = paratextproject;
            BookChapter = bookchapter;
            BeforeSync = beforesync;
        }
        public Paratextsync(int userid, int planid, string paratextproject, string bookchapter, string aftersync, string err) : base(userid)
        {
            PlanId = planid;
            ParatextProject = paratextproject;
            BookChapter = bookchapter;
            BeforeSync = aftersync;
            Err = err;
        }

        [Attr(PublicName="plan-id")]
        public int PlanId { get; set; }

        [Attr(PublicName="paratext-project")]
        public string? ParatextProject { get; set; }

        [Attr(PublicName="book-chapter")]
        public string? BookChapter { get; set; }

        [Attr(PublicName="before-sync")]
        public string? BeforeSync { get; set; }

        [Attr(PublicName="err")]
        public string? Err { get; set; }
    }
}
