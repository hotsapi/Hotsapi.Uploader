using System;
using System.Linq;

namespace HotsapiUploader.Common
{
    public struct UploadResult
    {
        public readonly string file;
        public readonly UploadStatus uploadStatus;

        public UploadResult(string file, UploadStatus uploadStatus)
        {
            this.file = file;
            this.uploadStatus = uploadStatus;
        }
    }
}
