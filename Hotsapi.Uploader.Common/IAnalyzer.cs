using Heroes.ReplayParser;

namespace Hotsapi.Uploader.Common
{
    public interface IAnalyzer
    {
        int MinimumBuild { get; set; }

        Replay Analyze(ReplayFile file);
        string GetFingerprint(Replay replay);
    }
}