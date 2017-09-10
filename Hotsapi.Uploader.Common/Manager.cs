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
using System.Diagnostics;

namespace Hotsapi.Uploader.Common
{
    public class Manager : INotifyPropertyChanged
    {
        public ObservableCollectionEx<ReplayFile> Files { get; private set; } = new ObservableCollectionEx<ReplayFile>();

        private static Logger _log = LogManager.GetCurrentClassLogger();
        private bool _initialized = false;
        private readonly AsyncAutoResetEvent _collectionUpdated = new AsyncAutoResetEvent();
        private readonly IReplayStorage _storage;
        private Uploader _uploader;
        private Analyzer _analyzer;
        private Monitor _monitor;

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
        public bool UploadToHotslogs
        {
            get {
                return _uploader?.UploadToHotslogs ?? false;
            }
            set {
                if (_uploader != null) {
                    _uploader.UploadToHotslogs = value;
                }
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
            _uploader = new Uploader();
            _analyzer = new Analyzer();
            _monitor = new Monitor();

            var replays = new List<ReplayFile>(_storage.Load());
            var lookup = new HashSet<ReplayFile>(replays);
            var comparer = new ReplayFile.ReplayFileComparer();
            replays.AddRange(_monitor.ScanReplays().Select(x => new ReplayFile(x)).Where(x => !lookup.Contains(x, comparer)));
            replays.OrderByDescending(x => x.Created).Map(x => Files.Add(x));

            _monitor.ReplayAdded += async (_, e) => {
                await EnsureFileAvailable(e.Data, 3000);
                Files.Insert(0, new ReplayFile(e.Data));
            };
            _monitor.Start();
            Files.CollectionChanged += (_, __) => _collectionUpdated.Set();

            _analyzer.MinimumBuild = await _uploader.GetMinimumBuild();

            while (true) {
                try {
                    // take files one by one, in case newer replays are added to the top of the list while we upload older ones
                    // upload in a single thread to prevent load spikes on server
                    var file = Files.Where(f => f.UploadStatus == UploadStatus.None).FirstOrDefault();
                    if (file != null) {
                        file.UploadStatus = UploadStatus.InProgress;

                        // test if replay is eligible for upload (not AI, PTR, Custom, etc)
                        _analyzer.Analyze(file);
                        if (file.UploadStatus == UploadStatus.InProgress) {
                            // if it is, upload it
                            await _uploader.Upload(file);
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
                    _log.Error(ex, "Error in upload loop");
                }
            }
        }

        /// <summary>
        /// Ensure that HotS client finished writing replay file and it can be safely open
        /// </summary>
        /// <param name="filename">Filename to test</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <param name="testWrite">Whether to test read or write access</param>
        public async Task EnsureFileAvailable(string filename, int timeout, bool testWrite = true)
        {
            var timer = new Stopwatch();
            timer.Start();
            while (timer.ElapsedMilliseconds < timeout) {
                try {
                    if (testWrite) {
                        File.OpenWrite(filename).Close();
                    } else {
                        File.OpenRead(filename).Close();
                    }
                    return;
                }
                catch (IOException) {
                    // File is still in use
                    await Task.Delay(100);
                }
                catch {
                    return;
                }
            }
        }
    }
}