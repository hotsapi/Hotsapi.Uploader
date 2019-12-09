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
using Heroes.ReplayParser;
using System.Collections.Concurrent;

namespace Hotsapi.Uploader.Common
{
    public class Manager : INotifyPropertyChanged
    {
        /// <summary>
        /// Maximum number of simultaneous uploads in progress
        /// </summary>
        public const int MaxUploads = 4;

        /// <summary>
        /// Replay list
        /// </summary>
        public ObservableCollectionEx<ReplayFile> Files { get; private set; } = new ObservableCollectionEx<ReplayFile>();

        private static Logger _log = LogManager.GetCurrentClassLogger();
        private bool _initialized = false;
        private AsyncCollection<ReplayFile> processingQueue = new AsyncCollection<ReplayFile>(new ConcurrentQueue<ReplayFile>());
        private ConcurrentAyncBuffer<(Replay, ReplayFile)> FingerprintingQueue = new ConcurrentAyncBuffer<(Replay, ReplayFile)>();
        private ConcurrentAyncBuffer<(Replay, ReplayFile)> UploadQueue = new ConcurrentAyncBuffer<(Replay, ReplayFile)>();
        private readonly IReplayStorage _storage;
        private IUploader _uploader;
        private IAnalyzer _analyzer;
        private IMonitor _monitor;

        public event PropertyChangedEventHandler PropertyChanged;

        private string _status = "";
        /// <summary>
        /// Current uploader status
        /// </summary>
        public string Status
        {
            get {
                return _status;
            }
        }

        private Dictionary<UploadStatus, int> _aggregates = new Dictionary<UploadStatus, int>();
        /// <summary>
        /// List of aggregate upload stats
        /// </summary>
        public Dictionary<UploadStatus, int> Aggregates
        {
            get {
                return _aggregates;
            }
        }

        /// <summary>
        /// Whether to mark replays for upload to hotslogs
        /// </summary>
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

        /// <summary>
        /// Which replays to delete after upload
        /// </summary>
        public DeleteFiles DeleteAfterUpload { get; set; }

        public Manager(IReplayStorage storage)
        {
            _storage = storage;
            Files.ItemPropertyChanged += (_, __) => { RefreshStatusAndAggregates(); };
            Files.CollectionChanged += (_, __) => { RefreshStatusAndAggregates(); };
        }

        /// <summary>
        /// Start uploading and watching for new replays
        /// </summary>
        public async void Start(IMonitor monitor, IAnalyzer analyzer, IUploader uploader)
        {
            if (_initialized) {
                return;
            }
            _initialized = true;

            _uploader = uploader;
            _analyzer = analyzer;
            _monitor = monitor;

            var replays = ScanReplays();
            Files.AddRange(replays.Reverse());
            replays.Where(x => x.UploadStatus == UploadStatus.None)
                   .Map(processingQueue.Add);

            _monitor.ReplayAdded += async (_, e) => {
                await EnsureFileAvailable(e.Data, 3000);
                var replay = new ReplayFile(e.Data);
                Files.Insert(0, replay);
                processingQueue.Add(replay);
            };
            _monitor.Start();

            _analyzer.MinimumBuild = await _uploader.GetMinimumBuild();

            _ = Task.Run(() => ParseLoop());
            _ = Task.Run(() => FingerprintLoop());
            _ = Task.Run(() => UploadLoop());
        }

        public void Stop()
        {
            _monitor.Stop();
            processingQueue.CompleteAdding();
        }

        private async Task ParseLoop()
        {
            //OutputAvailableAsync will keep returning true
            //untill all data is processed and processQueue.CompleteAdding is called

            var inFlight = new HashSet<ReplayFile>();
            var submissionBatch = new List<(Replay, ReplayFile)>();
            var l = new object();
            while (await processingQueue.OutputAvailableAsync()) {
                try {
                    var file = await processingQueue.TakeAsync();
                    lock (l) {
                        inFlight.Add(file);
                    }
                    //don't wait for completion of background pool task.
                    //it's internally limited to a fixed number of low-priority threads
                    //so we can throw as much work on there as we want without choking it

                    //don't submit files for fingerprinting if we have a younger file in-flight
                    _ = WorkerPool.RunBackground(async () => {
                        var replay = _analyzer.Analyze(file);
                        var doEnqueue = Task.CompletedTask;
                        lock (l) {
                            _ = inFlight.Remove(file);
                           
                            if (replay != null && file.UploadStatus == UploadStatus.Preprocessed) {
                                submissionBatch.Add((replay, file));
                                var youngestSubmit = submissionBatch.Select(rp => rp.Item2.Created).Min();
                                var youngerInFlight = inFlight.Any(rf => rf.Created < youngestSubmit);

                                if (!youngerInFlight) {
                                    doEnqueue = FingerprintingQueue.EnqueueManyAsync(submissionBatch);
                                    submissionBatch = new List<(Replay, ReplayFile)>();
                                }
                            }
                        }
                        await doEnqueue;
                    });
                }
                catch (Exception ex) {
                    _log.Error(ex, "Error in parse loop");
                }
            }
        }

        private async Task FingerprintLoop() {
            while (true) {
                //take batches from the fingerprinting queue, fingerprint
                //those with status Preprocessed are checked for duplicates
                //(should be all, but future concurrent processes could change that)
                //and those that aren't duplicates (have status ReadyForUpload)
                //are enqueued for upload
                var UnFingerprinted = await FingerprintingQueue.DequeueAsync();
                var eligible = UnFingerprinted.Where(pair => pair.Item2.UploadStatus == UploadStatus.Preprocessed).ToList();
                await _uploader.CheckDuplicate(eligible.Select(pair => pair.Item2));
                var read = eligible.Where(pair => pair.Item2.UploadStatus == UploadStatus.ReadyForUpload);
                await UploadQueue.EnqueueManyAsync(read.OrderBy(pair => pair.Item1.Timestamp));
            }
        }
        private async Task UploadLoop() {
            //Make sure that the next upload doesn't *end* before the previous ended
            //but it's OK for multiple uploads to run concurrently
            var previousDone = Task.CompletedTask;
            var l = new object();
            using (var rateLimitUploading = new SemaphoreSlim(MaxUploads)){
                while (true) {
                    var parsed = await UploadQueue.DequeueAsync();
                    foreach (var (replay, replayfile) in parsed) {
                        if (replayfile.UploadStatus == UploadStatus.ReadyForUpload) {
                            //don't await the upload task, but bound it by the upload ratelimiter
                            _ = rateLimitUploading.Locked(async () => {
                                Task thisDone;
                                lock (l) {
                                    thisDone = DoFileUpload(replayfile, replay, previousDone);
                                    previousDone = thisDone;
                                }
                                await thisDone;
                            });
                        }
                    }
                }
            }
        }


        private async Task DoFileUpload(ReplayFile file, Replay replay, Task mayComplete)
        {
            // Analyze will set the upload status as a side-effect when it's unsuitable for uploading
            if (file.UploadStatus == UploadStatus.ReadyForUpload) {
                await _uploader.Upload(file, mayComplete);
            }
            SaveReplayList();
            if (ShouldDelete(file, replay)) {
                DeleteReplay(file);
            }
        }

        private bool IsProcessingStatus(UploadStatus status) =>
            status == UploadStatus.Preprocessing ||
            status == UploadStatus.Preprocessed ||
            status == UploadStatus.ReadyForUpload ||
            status == UploadStatus.Uploading;

        private void RefreshStatusAndAggregates()
        {
            _status = Files.Select(x => x.UploadStatus).Any(IsProcessingStatus) ? "Processing..." : "Idle";
            _aggregates = Files.GroupBy(x => x.UploadStatus).ToDictionary(x => x.Key, x => x.Count());
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Aggregates)));
        }

        private void SaveReplayList()
        {
            try {
                // save only replays with fixed status. Will retry failed ones on next launch.
                var ignored = new[] { UploadStatus.None, UploadStatus.UploadError};
                bool isIgnored(UploadStatus status) => ignored.Contains(status) || IsProcessingStatus(status);
                _storage.Save(Files.Where(file => !isIgnored(file.UploadStatus)));
            }
            catch (Exception ex) {
                _log.Error(ex, "Error saving replay list");
            }
        }

        /// <summary>
        /// Load replay cache and merge it with folder scan results
        /// </summary>
        private IEnumerable<ReplayFile> ScanReplays()
        {
            var replays = new List<ReplayFile>(_storage.Load());
            var lookup = new HashSet<ReplayFile>(replays);
            var comparer = new ReplayFile.ReplayFileComparer();
            replays.AddRange(_monitor.ScanReplays().Select(x => new ReplayFile(x)).Where(x => !lookup.Contains(x, comparer)));
            return replays.OrderBy(x => x.Created).ToList();
        }

        /// <summary>
        /// Delete replay file
        /// </summary>
        private static void DeleteReplay(ReplayFile file)
        {
            try {
                _log.Info($"Deleting replay {file}");
                file.Deleted = true;
                File.Delete(file.Filename);
            }
            catch (Exception ex) {
                _log.Error(ex, "Error deleting file");
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

        /// <summary>
        /// Decide whether a replay should be deleted according to current settings
        /// </summary>
        /// <param name="file">replay file metadata</param>
        /// <param name="replay">Parsed replay</param>
        private bool ShouldDelete(ReplayFile file, Replay replay)
        {
            return
                DeleteAfterUpload.HasFlag(DeleteFiles.PTR) && file.UploadStatus == UploadStatus.PtrRegion ||
                DeleteAfterUpload.HasFlag(DeleteFiles.Ai) && file.UploadStatus == UploadStatus.AiDetected ||
                DeleteAfterUpload.HasFlag(DeleteFiles.Custom) && file.UploadStatus == UploadStatus.CustomGame ||
                file.UploadStatus == UploadStatus.Success && (
                    DeleteAfterUpload.HasFlag(DeleteFiles.Brawl) && replay.GameMode == GameMode.Brawl ||
                    DeleteAfterUpload.HasFlag(DeleteFiles.QuickMatch) && replay.GameMode == GameMode.QuickMatch ||
                    DeleteAfterUpload.HasFlag(DeleteFiles.UnrankedDraft) && replay.GameMode == GameMode.UnrankedDraft ||
                    DeleteAfterUpload.HasFlag(DeleteFiles.HeroLeague) && replay.GameMode == GameMode.HeroLeague ||
                    DeleteAfterUpload.HasFlag(DeleteFiles.TeamLeague) && replay.GameMode == GameMode.TeamLeague ||
                    DeleteAfterUpload.HasFlag(DeleteFiles.StormLeague) && replay.GameMode == GameMode.StormLeague
                );
        }
    }
}