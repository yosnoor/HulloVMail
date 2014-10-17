using Microsoft.WindowsAzure.Mobile.Service;
using System;

namespace HulloVMailMobileService.DataObjects
{
    public class HulloVMail : EntityData
    {
        public int HulloID { get; set; }
        public string From { get; set; }
        public DateTime RecordedDate { get; set; }
        public byte[] Message { get; set; }
        public bool IsNew { get; set; }
        public string EmailID { get; set; }
        public string FromDisplay { get; set; }
    }
}