using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Hotsapi.Uploader.Common
{
    [Serializable]
    public class ReplayFile : INotifyPropertyChanged
    {
        public string Filename { get; set; }

        [XmlIgnore]
        public string Fingerprint { get; set; }

        public DateTime Created { get; set; }

        UploadStatus _uploadStatus = UploadStatus.None;
        public UploadStatus UploadStatus
        {
            get {
                return _uploadStatus;
            }
            set {
                if (_uploadStatus == value) {
                    return;
                }

                _uploadStatus = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UploadStatus)));
            }
        }

        public ReplayFile() { } // Required for serialization

        public ReplayFile(string filename)
        {
            Filename = filename;
            Created = File.GetCreationTime(filename);
        }

        public override string ToString()
        {
            return Filename;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
