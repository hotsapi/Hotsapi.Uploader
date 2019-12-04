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
    public class Analyzer : IAnalyzer
    {
        public int MinimumBuild { get; set; }

        private static Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Analyze replay locally before uploading.
        /// 
        /// Sets file status as a side-effect.
        /// </summary>
        /// <param name="file">Replay file</param>
        public Replay Analyze(ReplayFile file)
        {
            try {
                file.UploadStatus = UploadStatus.Preprocessing;
                var (parseResult, replay) = DataParser.ParseReplay(file.Filename, false, false, false, true);
                file.UploadStatus = GetPreStatus(replay, parseResult) ?? file.UploadStatus;
                if (parseResult != DataParser.ReplayParseResult.Success) {
                    return null;
                }

                file.Fingerprint = GetFingerprint(replay);
                return replay;
            }
            catch (Exception e) {
                _log.Warn(e, $"Error analyzing file {file}");
                return null;
            }
        }

        private UploadStatus? GetPreStatus(Replay replay, DataParser.ReplayParseResult parseResult)
        {
            switch (parseResult) {
                case DataParser.ReplayParseResult.ComputerPlayerFound:
                case DataParser.ReplayParseResult.TryMeMode:
                    return UploadStatus.AiDetected;

                case DataParser.ReplayParseResult.PTRRegion:
                    return UploadStatus.PtrRegion;

                case DataParser.ReplayParseResult.PreAlphaWipe:
                    return UploadStatus.TooOld;
                case DataParser.ReplayParseResult.Incomplete:
                    return UploadStatus.Incomplete;
            }

            return parseResult != DataParser.ReplayParseResult.Success ? null
                 : replay.GameMode == GameMode.Custom ? (UploadStatus?)UploadStatus.CustomGame
                 : replay.ReplayBuild < MinimumBuild ? (UploadStatus?)UploadStatus.TooOld
                 : (UploadStatus?)UploadStatus.Preprocessed;
        }

        /// <summary>
        /// Get unique hash of replay. Compatible with HotsLogs
        /// </summary>
        /// <param name="replay"></param>
        /// <returns></returns>
        public string GetFingerprint(Replay replay)
        {
            var str = new StringBuilder();
            replay.Players.Select(p => p.BattleNetId).OrderBy(x => x).Map(x => str.Append(x.ToString()));
            str.Append(replay.RandomValue);
            var md5 = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str.ToString()));
            var result = new Guid(md5);
            return result.ToString();
        }

        /// <summary>
        /// Swaps two bytes in a byte array
        /// </summary>
        private void SwapBytes(byte[] buf, int i, int j)
        {
            byte temp = buf[i];
            buf[i] = buf[j];
            buf[j] = temp;
        }
    }
}
