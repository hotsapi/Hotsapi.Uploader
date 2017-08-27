using System;
using System.Collections.Generic;
using System.Linq;

namespace HotsapiUploader.Common
{
    public enum UploadStatus
    {
        None,
        Success,
        InProgress,
        UploadError,
        Duplicate,
        AiDetected,
        CustomGame,
        PtrRegion,
        TooOld,
    }
}
