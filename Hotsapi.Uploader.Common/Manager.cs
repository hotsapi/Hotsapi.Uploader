using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using NLog;
using Nito.AsyncEx;

namespace Hotsapi.Uploader.Common
{
    public class Manager : INotifyPropertyChanged
    {
        public ObservableCollectionEx<ReplayFile> Files { get; private set; } = new ObservableCollectionEx<ReplayFile>();

        private static Logger _log = LogManager.GetCurrentClassLogger();
        private bool _initialized = false;
        private readonly AsyncAutoResetEvent _collectionUpdated = new AsyncAutoResetEvent();
        private readonly IReplayStorage _storage;

        public event PropertyChangedEventHandler PropertyChanged;
        public string Status
        {
            get {
                return Files.Any(x => x.UploadStatus == UploadStatus.InProgress) ? "Uploading..." : "Idle";
            }
        }
        public Dictionary<UploadStatus, int> Aggregates
        {
            get {
                return Files.GroupBy(x => x.UploadStatus).ToDictionary(x => x.Key, x => x.Count());
            }
        }

        public Manager(IReplayStorage storage)
        {
            this._storage = storage;
            Files.ItemPropertyChanged += (_, __) => {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Aggregates)));
            };
            Files.CollectionChanged += (_, __) => {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Aggregates)));
            };
        }

        public void Start()
        {
            if (_initialized) {
                return;
            }
            _initialized = true;

            Task.Run(UploadLoop);
        }

        private async Task UploadLoop()
        {
            var uploader = new Uploader();
            var analyzer = new Analyzer();
            var monitor = new Monitor();

            _storage.Load().Map(x => Files.Add(x));
            var filenames = Files.Select(x => x.Filename).ToList();
            monitor.ScanReplays().Where(x => !filenames.Contains(x)).OrderByDescending(x => File.GetCreationTime(x)).Map(x => Files.Add(new ReplayFile(x)));

            monitor.ReplayAdded += (_, e) => Files.Insert(0, new ReplayFile(e.Data));
            monitor.Start();
            Files.CollectionChanged += (_, __) => _collectionUpdated.Set();

            analyzer.MinimumBuild = await uploader.GetMinimumBuild();

            while (true) {
                try {
                    // take files one by one, in case newer replays are added to the top of the list while we upload older ones
                    // upload in a single thread to prevent load spikes on server
                    var file = Files.Where(f => f.UploadStatus == UploadStatus.None).FirstOrDefault();
                    if (file != null) {
                        file.UploadStatus = UploadStatus.InProgress;
                        // todo check that file is not not still being written to (in use)

                        // test if game is vs AI or a custom game
                        analyzer.Analyze(file);
                        if (file.UploadStatus == UploadStatus.InProgress) {
                            // if not, upload it
                            await uploader.Upload(file);
                        }
                        try {
                            // save only replays with fixed status. Will retry failed ones on next launch.
                            _storage.Save(Files.Where(x => !new[] { UploadStatus.None, UploadStatus.UploadError, UploadStatus.InProgress }.Contains(x.UploadStatus)));
                        }
                        catch (Exception ex) {
                            // we can still continue uploading
                            _log.Error(ex, "Error saving replay list");
                        }
                    } else {
                        await _collectionUpdated.WaitAsync();
                    }
                }
                catch (Exception ex) {
                    _log.Fatal(ex, "Error in upload loop");
                    throw;
                }
            }
        }
    }
}
