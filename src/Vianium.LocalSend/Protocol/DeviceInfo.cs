// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using Vianium.LocalSend.Json;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage;

namespace Vianium.LocalSend.Protocol
{
    internal sealed class DeviceInfo
    {
        public string Alias { get; set; }
        public string Version { get; set; }
        public string DeviceModel { get; set; }
        public string DeviceType { get; set; }
        public string Fingerprint { get; set; }
        public int Port { get; set; }
        public string Protocol { get; set; }
        public bool Download { get; set; }
        public bool Announce { get; set; }

        public DeviceInfo()
        {
            Version = ProtocolConstants.Version;
            Port = ProtocolConstants.Port;
            Protocol = ProtocolConstants.Protocol;
            DeviceType = ProtocolConstants.DeviceType;
            Download = false;
            Announce = true;
        }

        public static DeviceInfo CreateLocal()
        {
            var info = new DeviceInfo();
            info.DeviceModel = GetDeviceModelName();
            info.Alias = GetSavedAlias() ?? info.DeviceModel ?? "Windows Phone";
            info.Fingerprint = GetOrCreateFingerprint();
            return info;
        }

        public JsonObject ToMulticastJson()
        {
            var obj = new JsonObject();
            obj.Set("alias", new JsonString(Alias));
            obj.Set("version", new JsonString(Version));
            if (DeviceModel != null)
                obj.Set("deviceModel", new JsonString(DeviceModel));
            obj.Set("deviceType", new JsonString(DeviceType));
            obj.Set("fingerprint", new JsonString(Fingerprint));
            obj.Set("port", new JsonNumber(Port));
            obj.Set("protocol", new JsonString(Protocol));
            obj.Set("download", Download ? JsonBoolean.True : JsonBoolean.False);
            obj.Set("announce", Announce ? JsonBoolean.True : JsonBoolean.False);
            return obj;
        }

        public JsonObject ToInfoJson()
        {
            var obj = new JsonObject();
            obj.Set("alias", new JsonString(Alias));
            obj.Set("version", new JsonString(Version));
            if (DeviceModel != null)
                obj.Set("deviceModel", new JsonString(DeviceModel));
            obj.Set("deviceType", new JsonString(DeviceType));
            obj.Set("fingerprint", new JsonString(Fingerprint));
            obj.Set("download", Download ? JsonBoolean.True : JsonBoolean.False);
            return obj;
        }

        public JsonObject ToRegisterJson()
        {
            var obj = new JsonObject();
            obj.Set("alias", new JsonString(Alias));
            obj.Set("version", new JsonString(Version));
            if (DeviceModel != null)
                obj.Set("deviceModel", new JsonString(DeviceModel));
            obj.Set("deviceType", new JsonString(DeviceType));
            obj.Set("fingerprint", new JsonString(Fingerprint));
            obj.Set("port", new JsonNumber(Port));
            obj.Set("protocol", new JsonString(Protocol));
            obj.Set("download", Download ? JsonBoolean.True : JsonBoolean.False);
            return obj;
        }

        public static DeviceInfo FromJson(JsonValue json)
        {
            if (!json.IsObject) return null;

            var info = new DeviceInfo();
            info.Alias = GetString(json, "alias", "Unknown");
            info.Version = GetString(json, "version", "1.0");
            info.DeviceModel = GetStringOrNull(json, "deviceModel");
            info.DeviceType = GetString(json, "deviceType", "desktop");
            info.Fingerprint = GetString(json, "fingerprint", "");
            info.Port = GetInt(json, "port", ProtocolConstants.Port);
            info.Protocol = GetString(json, "protocol", "https");
            info.Download = GetBool(json, "download", false);

            // "announce" field (multicast only)
            info.Announce = GetBool(json, "announce", false);

            return info;
        }

        private static string GetDeviceModelName()
        {
            try
            {
                var deviceInfo = new EasClientDeviceInformation();
                string manufacturer = deviceInfo.SystemManufacturer;
                string product = deviceInfo.SystemProductName;

                if (!string.IsNullOrEmpty(manufacturer) && !string.IsNullOrEmpty(product))
                    return manufacturer + " " + product;
                if (!string.IsNullOrEmpty(product))
                    return product;
                if (!string.IsNullOrEmpty(manufacturer))
                    return manufacturer;
            }
            catch { }
            return "Windows Phone";
        }

        private static string GetOrCreateFingerprint()
        {
            var settings = ApplicationData.Current.LocalSettings;
            object saved;
            if (settings.Values.TryGetValue("device_fingerprint", out saved) && saved is string)
            {
                return (string)saved;
            }

            string fingerprint = Crypto.FingerprintGenerator.Generate();
            settings.Values["device_fingerprint"] = fingerprint;
            return fingerprint;
        }

        public static string GetSavedAlias()
        {
            var settings = ApplicationData.Current.LocalSettings;
            object saved;
            if (settings.Values.TryGetValue("device_alias", out saved) && saved is string)
            {
                return (string)saved;
            }
            return null;
        }

        public static void SaveAlias(string alias)
        {
            ApplicationData.Current.LocalSettings.Values["device_alias"] = alias;
        }

        private static string GetString(JsonValue obj, string key, string defaultValue)
        {
            if (obj.ContainsKey(key) && obj[key].IsString)
                return obj[key].AsString();
            return defaultValue;
        }

        private static string GetStringOrNull(JsonValue obj, string key)
        {
            if (obj.ContainsKey(key) && obj[key].IsString)
                return obj[key].AsString();
            return null;
        }

        private static int GetInt(JsonValue obj, string key, int defaultValue)
        {
            if (obj.ContainsKey(key) && obj[key].IsNumber)
                return obj[key].AsInt();
            return defaultValue;
        }

        private static bool GetBool(JsonValue obj, string key, bool defaultValue)
        {
            if (obj.ContainsKey(key) && obj[key].IsBoolean)
                return obj[key].AsBool();
            return defaultValue;
        }
    }
}
