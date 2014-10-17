using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.WindowsAzure.Mobile.Service;

namespace HulloVMailMobileServiceMobileService.DataObjects
{
    public class Hullomail : EntityData
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