using HotsapiUploader.Common;
using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace HotsapiUploader.Windows.UIHelpers
{
    public class FilenameConverter : GenericValueConverter<string, string>
    {
        protected override string Convert(string value)
        {
            return Path.GetFileName(value);
        }
    }
}
