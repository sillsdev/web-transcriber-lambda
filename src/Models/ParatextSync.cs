

using JsonApiDotNetCore.Models;

namespace SIL.Logging.Models
{
    public partial class ParatextSync : LogBaseModel
    {
        public ParatextSync() : base()
        {
        }
        public ParatextSync(int userid, int planid, string paratextproject, string bookchapter, string beforesync) : base(userid)
        {
            PlanId = PlanId;
            ParatextProject = paratextproject;
            BookChapter = bookchapter;
            BeforeSync = beforesync;
        }
        public ParatextSync(int userid, int planid, string paratextproject, string bookchapter, string aftersync, string err) : base(userid)
        {
            PlanId = PlanId;
            ParatextProject = paratextproject;
            BookChapter = bookchapter;
            BeforeSync = aftersync;
            Err = err;
        }

        [Attr("plan-id")]
        public int PlanId { get; set; }

        [Attr("paratext-project")]
        public string ParatextProject { get; set; }

        [Attr("book-chapter")]
        public string BookChapter { get; set; }

        [Attr("before-sync")]
        public string BeforeSync { get; set; }

        [Attr("err")]
        public string Err { get; set; }
    }
}
