using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Logging.Models
{
    public partial class ParatextSyncPassage : LogBaseModel
    {
        public ParatextSyncPassage() : base()
        {
            Reference = "";
            Transcription = "";
            AfterSync = "";
        }
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
            AfterSync = "";
        }

        [Attr(PublicName="paratext-sync-id")]
        public int ParatextSyncId { get; set; }

        [Attr(PublicName="reference")]
        public string Reference { get; set; }

        [Attr(PublicName="transcription")]
        public string Transcription { get; set; }
        [Attr(PublicName="after-sync")]
        public string AfterSync { get; set; }
        [Attr(PublicName="err")]
        public string? Err { get; set; }
    }
}
