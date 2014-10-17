using System;

using Microsoft.WindowsAzure.Mobile.Service;

namespace HulloVMailManager.DataObjects
{
    public class VoicemailDTO : EntityData
    {
        public string From { get; set; }
        public DateTime RecordedDate { get; set; }
        public byte[] Message { get; set; }
        public bool IsNew { get; set; }
        public string EmailID { get; set; }
        public string FromDisplay { get; set; }
    }
}
