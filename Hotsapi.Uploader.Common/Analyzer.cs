using Heroes.ReplayParser;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common
{
    public class Analyzer
    {
        public int MinimumBuild { get; set; }

        private static Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Analyze replay locally before uploading
        /// </summary>
        /// <param name="file">Replay file</param>
        public void Analyze(ReplayFile file)
        {
            try {
                var result = DataParser.ParseReplay(file.Filename, false, false, false, true);
                switch (result.Item1) {
                    case DataParser.ReplayParseResult.ComputerPlayerFound:
                    case DataParser.ReplayParseResult.TryMeMode:
                        file.UploadStatus = UploadStatus.AiDetected;
                        return;

                    case DataParser.ReplayParseResult.PTRRegion:
                        file.UploadStatus = UploadStatus.PtrRegion;
                        return;

                    case DataParser.ReplayParseResult.PreAlphaWipe:
                        file.UploadStatus = UploadStatus.TooOld;
                        return;
                }

                if (result.Item1 != DataParser.ReplayParseResult.Success) {
                    return;
                }

                var replay = result.Item2;

                if (replay.ReplayBuild < MinimumBuild) {
                    file.UploadStatus = UploadStatus.TooOld;
                    return;
                }

                file.Fingerprint = GetFingerprint(replay);
            }
            catch (Exception e) {
                _log.Warn(e, $"Error analyzing file {file}");
            }
        }

        /// <summary>
        /// Get unique hash of replay. Compatible with HotsLogs
        /// </summary>
        /// <param name="replay"></param>
        /// <returns></returns>
        private string GetFingerprint(Replay replay)
        {
            var str = new StringBuilder();
            replay.Players.Select(p => p.BattleNetId).OrderBy(x => x).Map(x => str.Append(x.ToString()));
            str.Append(replay.RandomValue);
            var md5 = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str.ToString()));
            var result = new Guid(md5);
            return result.ToString();
        }
    }
}
