using JsonApiDotNetCore.Models;

namespace SIL.Logging.Models
{
    public partial class ParatextSyncPassage : LogBaseModel
    {
        public ParatextSyncPassage() : base()
        { }
        public ParatextSyncPassage(int userid, int paratextSyncId, string reference, string transcription, string afterSync) : base(userid)
        {
            ParatextSyncId = paratextSyncId;
            Reference = reference;
            Transcription = transcription;
            AfterSync = afterSync;
        }
        public ParatextSyncPassage(int userid,int paratextSyncId, string reference, string err) : base(userid)
        {
            ParatextSyncId = paratextSyncId;
            Reference = reference;
            Err = err;
            Transcription = "";
        }

        [Attr("paratext-sync-id")]
        public int ParatextSyncId { get; set; }

        [Attr("reference")]
        public string Reference { get; set; }

        [Attr("transcription")]
        public string Transcription { get; set; }
        [Attr("after-sync")]
        public string AfterSync { get; set; }
        [Attr("err")]
        public string Err { get; set; }
    }
}
