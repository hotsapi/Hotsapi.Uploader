﻿using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Common
{
    public interface IUploader
    {
        bool UploadToHotslogs { get; set; }
        Task CheckDuplicate(IEnumerable<ReplayFile> replays);
        Task<int> GetMinimumBuild();
        Task Upload(ReplayFile file);
        Task<UploadStatus> Upload(string file);
    }
}