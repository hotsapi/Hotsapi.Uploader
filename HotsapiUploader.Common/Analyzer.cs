using Heroes.ReplayParser;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotsapiUploader.Common
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
                var result = DataParser.ParseReplay(file.Filename, false, false);
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
                // todo get full replay fingerprint
                file.Fingerprint = replay.RandomValue.ToString();
            }
            catch (Exception e) {
                _log.Warn(e, $"Error analyzing file {file}");
            }
        }
    }
}
