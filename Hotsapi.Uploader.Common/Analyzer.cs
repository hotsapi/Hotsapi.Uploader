﻿using Heroes.ReplayParser;
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
        /// Analyze replay locally before uploading
        /// </summary>
        /// <param name="file">Replay file</param>
        public Replay Analyze(ReplayFile file)
        {
            try {
                var result = DataParser.ParseReplay(file.Filename, false, false, false, true);
                var replay = result.Item2;
                var parseResult = result.Item1;
                var status = GetPreStatus(replay, parseResult);

                if (status != null) {
                    file.UploadStatus = status.Value;
                }

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

        public UploadStatus? GetPreStatus(Replay replay, DataParser.ReplayParseResult parseResult)
        {
            switch (parseResult) {
                case DataParser.ReplayParseResult.ComputerPlayerFound:
                case DataParser.ReplayParseResult.TryMeMode:
                    return UploadStatus.AiDetected;

                case DataParser.ReplayParseResult.PTRRegion:
                    return UploadStatus.PtrRegion;

                case DataParser.ReplayParseResult.PreAlphaWipe:
                    return UploadStatus.TooOld;
            }

            if (parseResult != DataParser.ReplayParseResult.Success) {
                return null;
            }

            if (replay.GameMode == GameMode.Custom) {
                return UploadStatus.CustomGame;
            }

            if (replay.ReplayBuild < MinimumBuild) {
                return UploadStatus.TooOld;
            }

            return null;
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
