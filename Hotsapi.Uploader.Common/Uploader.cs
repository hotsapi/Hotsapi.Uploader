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
                byte[] response;
                using (var client = new WebClient()) {
                    response = await client.UploadFileTaskAsync($"{ApiEndpoint}/upload", file);
                }
                dynamic json = JObject.Parse(Encoding.UTF8.GetString(response));
                if ((bool)json.success) {
                    _log.Debug($"Uploaded file '{file}': {json.status}");
                    return (UploadStatus)Enum.Parse(typeof(UploadStatus), (string)json.status);
                } else {
                    _log.Warn($"Error uploading file '{file}': {json.message}");
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
                    response = await client.DownloadStringTaskAsync($"{ApiEndpoint}/replays/fingerprints/{fingerprint}");
                }
                dynamic json = JObject.Parse(response);
                return (bool)json.exists;
            }
            catch (WebException ex) {
                _log.Warn(ex, $"Error checking fingerprint '{fingerprint}'");
                return false;
            }
        }

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
