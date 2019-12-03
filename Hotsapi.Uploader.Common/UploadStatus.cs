using System;
using System.Collections.Generic;
using System.Linq;

namespace Hotsapi.Uploader.Common
{
    public enum UploadStatus
    {
        None,
        Success,
        Preprocessed,
        Preprocessing,
        Uploading,
        UploadError,
        Duplicate,
        AiDetected,
        CustomGame,
        PtrRegion,
        Incomplete,
        TooOld,
    }
}
