using Hotsapi.Uploader.Common;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace Hotsapi.Uploader.Windows.UIHelpers
{
    public class UploadStatusConverter : GenericValueConverter<IUploadStatus, string>
    {
        protected override string Convert(IUploadStatus value)
        {
            if (value == null) {
                return "Unhandled";
            }
            string unPascal(string pascalCased) => Regex.Replace(pascalCased, "([a-z])([A-Z])", m => $"{m.Groups[1].Value} {m.Groups[2].Value.ToLower()}");

            switch (value) {
                case UploadSuccess s: return (s.UploadID > 0) ? $"Uploaded({s.UploadID})"  : "Uploaded";
                case Rejected r: return unPascal(r.Reason.ToString());
                case InternalError err: return err.ErrorDescription;
                case IFailed f: return f.RawException.Message;
                case InProgress _: return "In Progress";
                default: return "Unknown";
            }
        }
    }
}
