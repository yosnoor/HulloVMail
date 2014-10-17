using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using Microsoft.WindowsAzure.Mobile.Service.Tables;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;

namespace HulloVMailManager.Models
{
    public class Voicemail
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Index(IsClustered = true)]
        [TableColumn(TableColumnType.CreatedAt)]
        public DateTimeOffset? CreatedAt { get; set; }

        [TableColumn(TableColumnType.Deleted)]
        public bool Deleted { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [TableColumn(TableColumnType.UpdatedAt)]
        public DateTimeOffset? UpdatedAt { get; set; }

        [TableColumn(TableColumnType.Version)]
        [Timestamp]
        public byte[] Version { get; set; }

        [Index]
        [TableColumn(TableColumnType.Id)]
        [MaxLength(36)]
        public string Id { get; set; }
        
        [Key]
        public int VoicemailID { get; set; }
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
        public VoicemailDBContext() : base("Name=MS_TableConnectionString")
        {
        }

        public DbSet<Voicemail> Voicemails { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Add(
                new AttributeToColumnAnnotationConvention<TableColumnAttribute, string>(
                    "ServiceTableColumn", (property, attributes) => attributes.Single().ColumnType.ToString()));
            base.OnModelCreating(modelBuilder);
        }

        public System.Data.Entity.DbSet<HulloVMailManager.DataObjects.VoicemailDTO> VoicemailDTOes { get; set; }
    }

    public class VoicemailInitializer : DropCreateDatabaseIfModelChanges<VoicemailDBContext>
    {}
}