using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common.Test
{
    public partial class ManagerTests
    {
        private class MockUploader : IUploader
        {
            public bool UploadToHotslogs { get; set; }

            public Func<ReplayFile, Task> UploadStarted { get; set; } = _ => Task.CompletedTask;
            public Func<ReplayFile, Task> UploadFinished { get; set; } = _ => Task.CompletedTask;

            public async Task CheckDuplicate(IEnumerable<ReplayFile> replays)
            {
                foreach (var replay in replays) {
                    replay.UploadStatus = UploadStatus.ReadyForUpload;
                }
                await ShortRandomDelay();
            }
            public Task<int> GetMinimumBuild() => Task.FromResult(1);
            public async Task Upload(ReplayFile file, Task mayComplete)
            {
                await UploadStarted(file);
                await Upload(file.Filename, mayComplete);
                await UploadFinished(file);
            }
            public async Task<UploadStatus> Upload(string file, Task mayComplete)
            {
                await ShortRandomDelay();
                await mayComplete;
                return UploadStatus.Success;
            }
        }
    }
}
