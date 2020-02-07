using Hotsapi.Uploader.Common;
using System.Windows.Media;

namespace Hotsapi.Uploader.Windows.UIHelpers
{
    public class UploadColorConverter : GenericValueConverter<IUploadStatus, Brush>
    {
        protected override Brush Convert(IUploadStatus value) =>
            value == null ? GetBrush("StatusUploadNeutralBrush") :
            value.IsSuccess ? GetBrush("StatusUploadSuccessBrush") :
            value == UploadStatus.InProgress ? GetBrush("StatusUploadInProgressBrush") :
            value is Rejected rej && rej.Reason != RejectionReason.Incomplete ? GetBrush("StatusUploadNeutralBrush") :
            GetBrush("StatusUploadFailedBrush");

        private Brush GetBrush(string key) => App.Current.Resources[key] as Brush;
    }
}
