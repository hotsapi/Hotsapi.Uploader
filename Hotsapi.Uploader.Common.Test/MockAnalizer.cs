﻿using Heroes.ReplayParser;

namespace Hotsapi.Uploader.Common.Test
{
    public partial class ManagerTests
    {
        private class MockAnalizer : IAnalyzer
        {
            public int MinimumBuild { get; set; }
            public Replay Analyze(ReplayFile file) => new Replay();
            public string GetFingerprint(Replay replay) => "dummy fingerprint";
        }
    }
}
