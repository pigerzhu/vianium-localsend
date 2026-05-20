// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using Windows.UI.Xaml.Data;

namespace Vianium.LocalSend.Converters
{
    internal sealed class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is long)
                return Format((long)value);
            if (value is int)
                return Format((int)value);
            if (value is double)
                return Format((long)(double)value);
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        public static string Format(long bytes)
        {
            if (bytes < 0) return "0 B";

            if (bytes < 1024)
                return bytes + " B";

            if (bytes < 1024 * 1024)
                return string.Format("{0:F1} KB", bytes / 1024.0);

            if (bytes < 1024L * 1024 * 1024)
                return string.Format("{0:F1} MB", bytes / (1024.0 * 1024));

            return string.Format("{0:F2} GB", bytes / (1024.0 * 1024 * 1024));
        }
    }
}
