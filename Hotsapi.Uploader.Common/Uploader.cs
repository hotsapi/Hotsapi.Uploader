using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common
{
    public class Uploader
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();
#if DEBUG
        const string ApiEndpoint = "http://hotsapi.local/api/v1";
#else
        const string ApiEndpoint = "http://hotsapi.net/api/v1";
#endif

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
        /// <param name="file"></param>
        public async Task Upload(ReplayFile file)
        {
            file.UploadStatus = UploadStatus.InProgress;
            if (file.Fingerprint != null && await CheckDuplicate(file.Fingerprint)) {
                _log.Debug($"File {file} marked as duplicate");
                file.UploadStatus = UploadStatus.Duplicate;
            } else {
                file.UploadStatus = await Upload(file.Filename);
            }
        }

        /// <summary>
        /// Upload replay
        /// </summary>
        /// <param name="file">Path to file</param>
        /// <returns>Upload result</returns>
        public async Task<UploadStatus> Upload(string file)
        {
            try {
                string response;
                using (var client = new WebClient()) {
                    var bytes = await client.UploadFileTaskAsync($"{ApiEndpoint}/upload?uploadToHotslogs={UploadToHotslogs}", file);
                    response = Encoding.UTF8.GetString(bytes);
                }
                dynamic json = JObject.Parse(response);
                if ((bool)json.success) {
                    if (Enum.TryParse<UploadStatus>((string)json.status, out UploadStatus status)) {
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
                _log.Warn(ex, $"Error uploading file '{file}'");
                return UploadStatus.UploadError;
            }
        }

        /// <summary>
        /// Check replay fingerprint against database to detect duplicate
        /// </summary>
        /// <param name="fingerprint"></param>
        public async Task<bool> CheckDuplicate(string fingerprint)
        {
            try {
                string response;
                using (var client = new WebClient()) {
                    response = await client.DownloadStringTaskAsync($"{ApiEndpoint}/replays/fingerprints/v2/{fingerprint}?uploadToHotslogs={UploadToHotslogs}");
                }
                dynamic json = JObject.Parse(response);
                return (bool)json.exists;
            }
            catch (WebException ex) {
                _log.Warn(ex, $"Error checking fingerprint '{fingerprint}'");
                return false;
            }
        }

        /// <summary>
        /// Mass check replay fingerprints against database to detect duplicates
        /// </summary>
        /// <param name="fingerprints"></param>
        public async Task<string[]> CheckDuplicate(IEnumerable<string> fingerprints)
        {
            try {
                string response;
                using (var client = new WebClient()) {
                    response = await client.UploadStringTaskAsync($"{ApiEndpoint}/replays/fingerprints?uploadToHotslogs={UploadToHotslogs}", String.Join("\n", fingerprints));
                }
                dynamic json = JObject.Parse(response);
                return (json.exists as JArray).Select(x => x.ToString()).ToArray();
            }
            catch (WebException ex) {
                _log.Warn(ex, $"Error checking fingerprint array");
                return new string[0];
            }
        }

        /// <summary>
        /// Mass check replay fingerprints against database to detect duplicates
        /// </summary>
        public async Task CheckDuplicate(IEnumerable<ReplayFile> replays)
        {
            var exists = new HashSet<string>(await CheckDuplicate(replays.Select(x => x.Fingerprint)));
            replays.Where(x => exists.Contains(x.Fingerprint)).Map(x => x.UploadStatus = UploadStatus.Duplicate);
        }

        /// <summary>
        /// Get minimum HotS client build supported by HotsApi
        /// </summary>
        public async Task<int> GetMinimumBuild()
        {
            try {
                using (var client = new WebClient()) {
                    var response = await client.DownloadStringTaskAsync($"{ApiEndpoint}/replays/min-build");
                    if (!int.TryParse(response, out int build)) {
                        _log.Warn($"Error parsing minimum build: {response}");
                        return 0;
                    }
                    return build;
                }
            }
            catch (WebException ex) {
                _log.Warn(ex, $"Error getting minimum build");
                return 0;
            }
        }
    }
}
