// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using Vianium.LocalSend.Json;

namespace Vianium.LocalSend.Protocol
{
    internal sealed class FileDto
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public long Size { get; set; }
        public string FileType { get; set; }
        public string Hash { get; set; }
        public string Preview { get; set; }

        public JsonObject ToJson()
        {
            var obj = new JsonObject();
            obj.Set("id", new JsonString(Id));
            obj.Set("fileName", new JsonString(FileName));
            obj.Set("size", new JsonNumber(Size));
            obj.Set("fileType", new JsonString(FileType ?? "application/octet-stream"));
            if (Hash != null)
                obj.Set("hash", new JsonString(Hash));
            else
                obj.Set("hash", JsonNull.Instance);
            if (Preview != null)
                obj.Set("preview", new JsonString(Preview));
            else
                obj.Set("preview", JsonNull.Instance);
            return obj;
        }

        public static FileDto FromJson(string id, JsonValue json)
        {
            if (!json.IsObject) return null;

            var dto = new FileDto();
            dto.Id = id;
            dto.FileName = GetString(json, "fileName", "unknown");
            dto.Size = GetLong(json, "size", 0);
            dto.FileType = GetString(json, "fileType", "application/octet-stream");
            dto.Hash = GetStringOrNull(json, "hash");
            dto.Preview = GetStringOrNull(json, "preview");
            return dto;
        }

        public static string GuessMimeType(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "application/octet-stream";

            string lower = fileName.ToLowerInvariant();
            int dot = lower.LastIndexOf('.');
            if (dot < 0) return "application/octet-stream";

            string ext = lower.Substring(dot);
            switch (ext)
            {
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                case ".svg": return "image/svg+xml";
                case ".webp": return "image/webp";
                case ".mp4": return "video/mp4";
                case ".avi": return "video/x-msvideo";
                case ".mkv": return "video/x-matroska";
                case ".mov": return "video/quicktime";
                case ".wmv": return "video/x-ms-wmv";
                case ".mp3": return "audio/mpeg";
                case ".wav": return "audio/wav";
                case ".ogg": return "audio/ogg";
                case ".flac": return "audio/flac";
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case ".ppt": return "application/vnd.ms-powerpoint";
                case ".pptx": return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                case ".zip": return "application/zip";
                case ".rar": return "application/x-rar-compressed";
                case ".7z": return "application/x-7z-compressed";
                case ".txt": return "text/plain";
                case ".html": case ".htm": return "text/html";
                case ".css": return "text/css";
                case ".js": return "application/javascript";
                case ".json": return "application/json";
                case ".xml": return "application/xml";
                case ".apk": return "application/vnd.android.package-archive";
                default: return "application/octet-stream";
            }
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

        private static long GetLong(JsonValue obj, string key, long defaultValue)
        {
            if (obj.ContainsKey(key) && obj[key].IsNumber)
                return obj[key].AsLong();
            return defaultValue;
        }
    }
}
