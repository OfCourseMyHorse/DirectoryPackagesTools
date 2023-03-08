using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

using NuGet.Versioning;

namespace DirectoryPackagesTools
{
    internal class VersionRangeShortTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is String text) return text;

            if (value is VersionRange versionRange) return versionRange.ToShortString();

            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text) return VersionRange.Parse(text);

            return null;
        }
    }
}
