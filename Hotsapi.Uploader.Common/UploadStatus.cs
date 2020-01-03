using System;
using System.Collections.Generic;
using System.Linq;

namespace Hotsapi.Uploader.Common
{
    public enum UploadStatus
    {
        None,
        Success,
        Preprocessing,
        Preprocessed,
        ReadyForUpload,
        Uploading,
        UploadError,
        CheckingDuplicates,
        Duplicate,
        AiDetected,
        CustomGame,
        PtrRegion,
        Incomplete,
        TooOld,
        
    }
}
