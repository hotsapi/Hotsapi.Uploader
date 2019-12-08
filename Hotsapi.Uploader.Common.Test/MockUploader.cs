﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common.Test
{
    public partial class ManagerTests
    {
        private class MockUploader : IUploader
        {
            public bool UploadToHotslogs { get; set; }

            private Func<ReplayFile, Task> UploadCallback = _ => Task.CompletedTask;
            public void SetUploadCallback(Func<ReplayFile, Task> onUpload)
            {
                var old = UploadCallback;

                UploadCallback = async (ReplayFile file) => {
                    await old(file);
                    await onUpload(file);
                };
            }

            public Task CheckDuplicate(IEnumerable<ReplayFile> replays) => Task.CompletedTask;
            public Task<int> GetMinimumBuild() => Task.FromResult(1);
            public Task Upload(ReplayFile file)
            {
                UploadCallback(file);
                return Task.CompletedTask;
            }
            public async Task<UploadStatus> Upload(string file)
            {
                await Task.Delay(100);
                return UploadStatus.Success;
            }
        }
    }
}
