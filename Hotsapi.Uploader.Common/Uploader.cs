using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common
{
    public class Uploader : IUploader
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
#if DEBUG
        private const string ApiEndpoint = "http://hotsapi.local/api/v1";
#else
        const string ApiEndpoint = "https://hotsapi.net/api/v1";
#endif

        private string UploadUrl => $"{ApiEndpoint}/upload?uploadToHotslogs={UploadToHotslogs}";
        private string BulkFingerprintUrl => $"{ApiEndpoint}/replays/fingerprints?uploadToHotslogs={UploadToHotslogs}";
        private string FingerprintOneUrl(string fingerprint) => $"{ApiEndpoint}/replays/fingerprints/v3/{fingerprint}?uploadToHotslogs={UploadToHotslogs}";


        public bool UploadToHotslogs { get; set; }

        /// <summary>
        /// New instance of replay uploader
        /// </summary>
        public Uploader()
        {

        }

        /// <summary>
        /// Upload replay
        /// </summary>
        /// <param name="file">The file to upload</param>
        public async Task Upload(ReplayFile file, Task mayComplete)
        {
            var doDuplicateCheck = file.UploadStatus != UploadStatus.ReadyForUpload;
            file.UploadStatus = UploadStatus.Uploading;
            if (file.Fingerprint != null && doDuplicateCheck && await CheckDuplicate(file.Fingerprint)) {
                _log.Debug($"File {file} marked as duplicate");
                file.UploadStatus = UploadStatus.Duplicate;
            } else {
                file.UploadStatus = await Upload(file.Filename, mayComplete);
            }
        }

        private class ReplayUpload : HttpContent
        {
            private const int defaultBuffersize = 1024;
            private readonly string filename;
            private readonly int buffersize;
            private readonly Task mayComplete;

            public ReplayUpload(string filename, int buffersize, Task mayComplete)
            {
                this.filename = filename;
                this.buffersize = buffersize;
                this.mayComplete = mayComplete;
            }

            public ReplayUpload(string filename, int buffersize) : this(filename, buffersize, Task.CompletedTask) { }
            public ReplayUpload(string filename, Task canComplete) : this(filename, defaultBuffersize, canComplete) { }
            public ReplayUpload(string filename) : this(filename, defaultBuffersize, Task.CompletedTask) { }


            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                using (var input = File.OpenRead(filename)) {
                    var buffer = new byte[buffersize];
                    var i = 0;
                    var done = false;
                    while (!done) {
                        var availableSpace = buffer.Length - i;
                        var bytesRead = await input.ReadAsync(buffer, i, availableSpace);
                        if (availableSpace > bytesRead) {
                            done = true;
                            await mayComplete;
                        }
                        await stream.WriteAsync(buffer, i, bytesRead);
                        i = 0;
                    }
                    await stream.FlushAsync();
                    stream.Close();
                    input.Close();
                }
            }
            protected override bool TryComputeLength(out long length)
            {
                length = -1;
                return false;
            }
        }

        /// <summary>
        /// Upload replay
        /// </summary>
        /// <param name="file">Path to file</param>
        /// <returns>Upload result</returns>
        public async Task<UploadStatus> Upload(string file, Task mayComplete)
        {
            try {
                string response;
                using (var client = new HttpClient()) {
                    var upload = new ReplayUpload(file, mayComplete);
                    var responseMessage = await client.PostAsync(UploadUrl, upload);
                    response = await responseMessage.Content.ReadAsStringAsync();
                }
                dynamic json = JObject.Parse(response);
                if ((bool)json.success) {
                    if (Enum.TryParse<UploadStatus>((string)json.status, out var status)) {
                        _log.Debug($"Uploaded file '{file}': {status}");
                        return status;
                    } else {
                        _log.Error($"Unknown upload status '{file}': {json.status}");
                        return UploadStatus.UploadError;
                    }
                } else {
                    _log.Warn($"Error uploading file '{file}': {response}");
                    return UploadStatus.UploadError;
                }
            }
            catch (WebException ex) {
                if (await CheckApiThrottling(ex.Response)) {
                    return await Upload(file, mayComplete);
                }
                _log.Warn(ex, $"Error uploading file '{file}'");
                return UploadStatus.UploadError;
            }
        }

        /// <summary>
        /// Check replay fingerprint against database to detect duplicate
        /// </summary>
        /// <param name="fingerprint"></param>
        private async Task<bool> CheckDuplicate(string fingerprint)
        {
            try {
                string response;
                using (var client = new WebClient()) {
                    response = await client.DownloadStringTaskAsync(FingerprintOneUrl(fingerprint));
                }
                dynamic json = JObject.Parse(response);
                return (bool)json.exists;
            }
            catch (WebException ex) {
                if (await CheckApiThrottling(ex.Response)) {
                    return await CheckDuplicate(fingerprint);
                }
                _log.Warn(ex, $"Error checking fingerprint '{fingerprint}'");
                return false;
            }
        }



        /// <summary>
        /// Mass check replay fingerprints against database to detect duplicates
        /// </summary>
        /// <param name="fingerprints"></param>
        private async Task<string[]> CheckDuplicate(IEnumerable<string> fingerprints)
        {
            try {
                string response;
                using (var client = new WebClient()) {
                    response = await client.UploadStringTaskAsync(BulkFingerprintUrl, String.Join("\n", fingerprints));
                }
                dynamic json = JObject.Parse(response);
                return (json.exists as JArray).Select(x => x.ToString()).ToArray();
            }
            catch (WebException ex) {
                if (await CheckApiThrottling(ex.Response)) {
                    return await CheckDuplicate(fingerprints);
                }
                _log.Warn(ex, $"Error checking fingerprint array");
                return Array.Empty<string>();
            }
        }



        /// <summary>
        /// Mass check replay fingerprints against database to detect duplicates
        /// </summary>
        public async Task CheckDuplicate(IEnumerable<ReplayFile> replays)
        {
            foreach (var replay in replays) {
                replay.UploadStatus = UploadStatus.CheckingDuplicates;
            }
            var exists = new HashSet<string>(await CheckDuplicate(replays.Select(x => x.Fingerprint)));
            foreach (var replay in replays) {
                replay.UploadStatus = exists.Contains(replay.Fingerprint) ? UploadStatus.Duplicate : UploadStatus.ReadyForUpload;
            }
        }

        /// <summary>
        /// Get minimum HotS client build supported by HotsApi
        /// </summary>
        public async Task<int> GetMinimumBuild()
        {
            try {
                using (var client = new WebClient()) {
                    var response = await client.DownloadStringTaskAsync($"{ApiEndpoint}/replays/min-build");
                    if (!Int32.TryParse(response, out var build)) {
                        _log.Warn($"Error parsing minimum build: {response}");
                        return 0;
                    }
                    return build;
                }
            }
            catch (WebException ex) {
                if (await CheckApiThrottling(ex.Response)) {
                    return await GetMinimumBuild();
                }
                _log.Warn(ex, $"Error getting minimum build");
                return 0;
            }
        }

        /// <summary>
        /// Check if Hotsapi request limit is reached and wait if it is
        /// </summary>
        /// <param name="response">Server response to examine</param>
        private static async Task<bool> CheckApiThrottling(WebResponse response)
        {
            if (response != null && (int)(response as HttpWebResponse).StatusCode == 429) {
                _log.Warn($"Too many requests, waiting");
                await Task.Delay(10000);
                return true;
            } else {
                return false;
            }
        }
    }
}
