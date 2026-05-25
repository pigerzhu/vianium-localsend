// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Globalization;
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Vianium.LocalSend.Converters
{
    public sealed class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                var hex = value as string;
                if (string.IsNullOrEmpty(hex))
                    return new SolidColorBrush(Color.FromArgb(255, 27, 161, 226));

                hex = hex.TrimStart('#');
                byte a = 255, r = 0, g = 0, b = 0;

                if (hex.Length == 8)
                {
                    a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                    b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                }
                else if (hex.Length == 6)
                {
                    r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                }
                return new SolidColorBrush(Color.FromArgb(a, r, g, b));
            }
            catch
            {
                return new SolidColorBrush(Color.FromArgb(255, 27, 161, 226));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
