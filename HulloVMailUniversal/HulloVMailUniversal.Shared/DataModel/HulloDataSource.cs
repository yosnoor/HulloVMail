using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace HulloVMailUniversal.DataModel
{
    /// <summary>
    /// Creates a collection of groups and items with content read from a static
    /// json file.
    /// 
    /// VoicemailDataSource initializes with data read from a static json file
    /// included in the project.  This provides sample data at both design-time
    /// and run-time.
    /// </summary>
    public class HulloDataSource
    {
        private static HulloDataSource _hulloDataSource = new HulloDataSource();

        private ObservableCollection<HulloData> _hullos = new ObservableCollection<HulloData>();
        public ObservableCollection<HulloData> Hullos
        {
            get { return this._hullos; }
        }

        public static async Task<IEnumerable<HulloData>> GetHullosAsync()
        {
            await _hulloDataSource.GetHulloDataAsync();

            return _hulloDataSource.Hullos;
        }

        public static async Task<HulloData> GetHulloAsync(int id)
        {
            await _hulloDataSource.GetHulloDataAsync();
            // Simple linear search is acceptable for small data sets
            var matches = _hulloDataSource.Hullos.Where((hullo) => hullo.Id.Equals(id));
            if (matches.Count() == 1) return matches.First();
            return null;
        }

        private async Task GetHulloDataAsync()
        {
            if (this._hullos.Count != 0)
                return;

            Uri dataUri = new Uri("ms-appx:///DataModel/HulloData.json");

            Windows.Storage.StorageFile file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(dataUri);
            string jsonText = await Windows.Storage.FileIO.ReadTextAsync(file);
            var jsonObject = JsonObject.Parse(jsonText);
            var jsonArray = jsonObject["Hullos"].GetArray();

            foreach (var hulloValue in jsonArray)
            {
                    var itemObject = hulloValue.GetObject();
                    this.Hullos.Add(new HulloData(Int32.Parse(itemObject["Id"].GetString()),
                                                   itemObject["From"].GetString(),
                                                   DateTime.Parse(itemObject["RecordedDate"].GetString()),
                                                   Encoding.UTF8.GetBytes(itemObject["message"].GetString()),
                                                   itemObject["IsNew"].GetBoolean(),
                                                   itemObject["EmailId"].GetString(),
                                                   itemObject["FromDisplay"].GetString()));
            }
        }
    }

    /// <summary>
    /// Generic item data model.
    /// </summary>
    public class HulloData
    {
        public HulloData(int id, String from, DateTime recordedDate,
                            byte[] message, bool isNew, String emailId,
                            string fromDisplay)
        {
            this.Id = id;
            this.From = from;
            this.RecordedDate = recordedDate;
            this.Message = message;
            this.IsNew = isNew;
            this.EmailId = emailId;
            this.FromDisplay = fromDisplay;
        }

        public int Id { get; private set; }
        public string From { get; private set; }
        public DateTime RecordedDate { get; private set; }
        public byte[] Message { get; private set; }
        public bool IsNew { get; private set; }
        public string EmailId { get; private set; }
        public string FromDisplay { get; private set; }

        public override string ToString()
        {
            return this.FromDisplay;
        }
    }
}