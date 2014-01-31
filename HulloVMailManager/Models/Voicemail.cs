using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace HulloVMailManager.Models
{
    public class Voicemail
    {
        public int ID { get; set; }
        public string From { get; set; }
        public DateTime RecordedDate { get; set; }
        [MaxLength]
        public byte[] Message { get; set; }
        public bool IsNew { get; set; }
        public string EmailID { get; set; }
        public string FromDisplay { get; set; }
    }

    public class VoicemailDBContext : DbContext
    {
        public DbSet<Voicemail> Voicemails { get; set; }
    }

    public class VoicemailInitializer : DropCreateDatabaseIfModelChanges<VoicemailDBContext>
    {}
}