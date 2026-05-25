// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Vianium.LocalSend.Converters;
using Vianium.LocalSend.Json;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Strings;

namespace Vianium.LocalSend.Data
{
    internal sealed class HistoryEntry
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string SenderAlias { get; set; }
        public string SenderModel { get; set; }
        public DateTime Timestamp { get; set; }
        public string SavedPath { get; set; }
        public bool IsSent { get; set; }

        // UI binding
        public string SizeText { get { return FileSizeConverter.Format(FileSize); } }
        public string TimeText { get { return Timestamp.ToLocalTime().ToString("dd/MM HH:mm"); } }
        public string DirectionIcon { get { return IsSent ? "\uE11C" : "\uE118"; } }
        public string DirectionText { get { return IsSent ? S.SentTo(SenderAlias) : S.ReceivedFrom(SenderAlias); } }
        public string FileIcon
        {
            get
            {
                if (FileName == null) return "\uE160";
                string lower = FileName.ToLowerInvariant();
                if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png") ||
                    lower.EndsWith(".gif") || lower.EndsWith(".bmp"))
                    return "\uE114";
                if (lower.EndsWith(".mp4") || lower.EndsWith(".avi") || lower.EndsWith(".mkv") ||
                    lower.EndsWith(".mov"))
                    return "\uE116";
                if (lower.EndsWith(".mp3") || lower.EndsWith(".wav") || lower.EndsWith(".flac"))
                    return "\uE189";
                if (lower.EndsWith(".pdf"))
                    return "\uE130";
                return "\uE160";
            }
        }
    }

    internal static class TransferHistory
    {
        private const string TAG = "History";
        private const string HistoryFileName = "transfer_history.json";
        private const int MaxEntries = 100;
        private static List<HistoryEntry> _entries;
        private static readonly object _lock = new object();

        public static async Task<List<HistoryEntry>> LoadAsync()
        {
            if (_entries != null) return _entries;

            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                StorageFile file = null;
                var files = await folder.GetFilesAsync();
                foreach (var candidate in files)
                {
                    if (candidate != null &&
                        string.Equals(candidate.Name, HistoryFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        file = candidate;
                        break;
                    }
                }

                if (file == null)
                {
                    _entries = new List<HistoryEntry>();
                    AppLog.I(TAG, "History loaded: 0 entries");
                    return _entries;
                }

                string content = await FileIO.ReadTextAsync(file);

                JsonValue json;
                if (SimpleJsonParser.TryParse(content, out json) && json.IsArray)
                {
                    _entries = new List<HistoryEntry>();
                    for (int i = 0; i < json.Count; i++)
                    {
                        var item = json[i];
                        if (!item.IsObject) continue;

                        _entries.Add(new HistoryEntry
                        {
                            FileName = item.ContainsKey("f") ? item["f"].AsString() : "unknown",
                            FileSize = item.ContainsKey("s") ? item["s"].AsLong() : 0,
                            SenderAlias = item.ContainsKey("a") ? item["a"].AsString() : S.UnknownDevice,
                            SenderModel = item.ContainsKey("m") ? item["m"].AsString() : "",
                            Timestamp = item.ContainsKey("t") ? DateTime.FromBinary(item["t"].AsLong()) : DateTime.MinValue,
                            SavedPath = item.ContainsKey("p") ? item["p"].AsString() : null,
                            IsSent = item.ContainsKey("d") && item["d"].AsBool()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Could not load history (starting empty): " + ex.Message);
            }

            if (_entries == null)
                _entries = new List<HistoryEntry>();

            AppLog.I(TAG, "History loaded: " + _entries.Count + " entries");
            return _entries;
        }

        public static async Task AddEntryAsync(HistoryEntry entry)
        {
            var entries = await LoadAsync();
            lock (_lock)
            {
                entries.Insert(0, entry);
                while (entries.Count > MaxEntries)
                    entries.RemoveAt(entries.Count - 1);
            }
            AppLog.V(TAG, "History + " + (entry != null ? entry.FileName : "(null)") +
                (entry != null && entry.IsSent ? " [sent]" : " [received]"));
            await SaveAsync();
        }

        public static async Task AddReceivedFilesAsync(Protocol.TransferSession session)
        {
            foreach (var kvp in session.Files)
            {
                if (kvp.Value.Status == Protocol.FileTransferStatus.Completed)
                {
                    await AddEntryAsync(new HistoryEntry
                    {
                        FileName = kvp.Value.File.FileName,
                        FileSize = kvp.Value.File.Size,
                        SenderAlias = session.RemoteDevice != null ? session.RemoteDevice.Alias : S.UnknownDevice,
                        SenderModel = session.RemoteDevice != null ? session.RemoteDevice.DeviceModel : "",
                        Timestamp = DateTime.UtcNow,
                        SavedPath = kvp.Value.SavedPath,
                        IsSent = false
                    });
                }
            }
        }

        public static async Task AddSentFilesAsync(string targetAlias, List<Protocol.FileDto> files)
        {
            foreach (var file in files)
            {
                await AddEntryAsync(new HistoryEntry
                {
                    FileName = file.FileName,
                    FileSize = file.Size,
                    SenderAlias = targetAlias,
                    Timestamp = DateTime.UtcNow,
                    IsSent = true
                });
            }
        }

        public static async Task ClearAsync()
        {
            lock (_lock)
            {
                if (_entries != null)
                    _entries.Clear();
            }
            AppLog.I(TAG, "History cleared by user");
            await SaveAsync();
        }

        private static async Task SaveAsync()
        {
            try
            {
                var arr = new JsonArray();
                List<HistoryEntry> snapshot;
                lock (_lock)
                {
                    snapshot = new List<HistoryEntry>(_entries ?? new List<HistoryEntry>());
                }

                foreach (var e in snapshot)
                {
                    var obj = new JsonObject();
                    obj.Set("f", new JsonString(e.FileName ?? ""));
                    obj.Set("s", new JsonNumber(e.FileSize));
                    obj.Set("a", new JsonString(e.SenderAlias ?? ""));
                    obj.Set("m", new JsonString(e.SenderModel ?? ""));
                    obj.Set("t", new JsonNumber(e.Timestamp.ToBinary()));
                    if (e.SavedPath != null)
                        obj.Set("p", new JsonString(e.SavedPath));
                    obj.Set("d", e.IsSent ? JsonBoolean.True : JsonBoolean.False);
                    arr.Add(obj);
                }

                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync(HistoryFileName,
                    CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, SimpleJsonParser.Serialize(arr));
                AppLog.V(TAG, "History saved: " + snapshot.Count + " entries");
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "Failed to save history", ex);
            }
        }
    }
}
