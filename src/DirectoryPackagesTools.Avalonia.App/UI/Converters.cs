using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Data.Converters;

using NuGet.Versioning;

namespace DirectoryPackagesTools
{
    internal class VersionRangeShortTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            switch (value)
            {
                case string text: return text;
                case NuGetVersion version: return version.ToNormalizedString();
                case VersionRange versionRange: return versionRange.ToShortString();
                default: return value?.ToString() ?? string.Empty;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text) return VersionRange.Parse(text);

            return null;
        }
    }
}
