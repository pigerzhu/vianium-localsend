// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using Windows.Security.Cryptography;
using Windows.Storage;

namespace Vianium.LocalSend.Data
{
    internal static class AppPreferences
    {
        private const string QuickSaveKey = "QuickSave";
        private const string RequirePinKey = "RequirePin";
        private const string AutostartKey = "Autostart";
        private const string ReceivePinKey = "ReceivePin";

        public static bool QuickSave
        {
            get { return GetBool(QuickSaveKey, false); }
            set { SetBool(QuickSaveKey, value); }
        }

        public static bool RequirePin
        {
            get { return GetBool(RequirePinKey, false); }
            set { SetBool(RequirePinKey, value); }
        }

        public static bool Autostart
        {
            get { return GetBool(AutostartKey, true); }
            set { SetBool(AutostartKey, value); }
        }

        public static string EnsureReceivePin()
        {
            object value;
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(ReceivePinKey, out value)
                && value is string
                && !string.IsNullOrWhiteSpace((string)value))
            {
                return (string)value;
            }

            return RegenerateReceivePin();
        }

        public static string RegenerateReceivePin()
        {
            string pin = GenerateReceivePin();
            ApplicationData.Current.LocalSettings.Values[ReceivePinKey] = pin;
            return pin;
        }

        private static bool GetBool(string key, bool defaultValue)
        {
            object value;
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out value)
                && value is bool
                ? (bool)value
                : defaultValue;
        }

        private static void SetBool(string key, bool value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }

        private static string GenerateReceivePin()
        {
            var random = CryptographicBuffer.GenerateRandom(4);
            byte[] bytes;
            CryptographicBuffer.CopyToByteArray(random, out bytes);
            uint value = BitConverter.ToUInt32(bytes, 0) % 1000000;
            return value.ToString("D6");
        }
    }
}
