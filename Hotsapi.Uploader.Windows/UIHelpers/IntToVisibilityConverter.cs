using Hotsapi.Uploader.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Hotsapi.Uploader.Windows.UIHelpers
{
    public class IntToVisibilityConverter : GenericValueConverter<Dictionary<IUploadStatus, int>, Visibility, IUploadStatus>
    {
        protected override Visibility Convert(Dictionary<IUploadStatus, int> value, IUploadStatus parameter)
        {
            return value.ContainsKey(parameter) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
