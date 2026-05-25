// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Threading.Tasks;
using Windows.Storage;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Strings;

namespace Vianium.LocalSend.Data
{
    /// <summary>
    /// Where to save each file type.
    /// Images → PicturesLibrary\LocalSend, Videos → VideosLibrary\LocalSend,
    /// Music → MusicLibrary\LocalSend, Others → C:\Data\Users\Public\LocalSend (interop) or LocalFolder fallback.
    /// </summary>
    internal static class StorageConfig
    {
        private const string TAG = "Storage";

        // Interop paths to try — in order of preference
        // D:\ = SD card (devices with external storage)
        // C:\Data\Users\Public\ = internal user storage (next to Pictures, Videos, Music)
        private static readonly string[] InteropRoots = new[]
        {
            @"C:\Data\Users\Public",
            @"D:\",
        };

        // Setting keys
        private const string KeyImageDest = "save_images_to";
        private const string KeyVideoDest = "save_videos_to";
        private const string KeyAudioDest = "save_audio_to";
        private const string KeyOtherDest = "save_other_to";

        // Destination options
        public const string DestLibrary = "library";   // KnownFolders or interop (visible to user)
        public const string DestSdCard = "sd";          // D:\LocalSend (SD card via interop)
        public const string DestLocal = "local";        // LocalFolder\LocalSend (app-only)

        // SD card detection cache
        private static bool _sdChecked;
        private static bool _sdAvailable;

        public static string GetImageDestination()
        {
            return GetSetting(KeyImageDest, DestLibrary);
        }

        public static string GetVideoDestination()
        {
            return GetSetting(KeyVideoDest, DestLibrary);
        }

        public static string GetAudioDestination()
        {
            return GetSetting(KeyAudioDest, DestLibrary);
        }

        public static string GetOtherDestination()
        {
            return GetSetting(KeyOtherDest, DestLibrary);
        }

        public static void SetImageDestination(string dest)
        {
            SetSetting(KeyImageDest, dest);
        }

        public static void SetVideoDestination(string dest)
        {
            SetSetting(KeyVideoDest, dest);
        }

        public static void SetAudioDestination(string dest)
        {
            SetSetting(KeyAudioDest, dest);
        }

        public static void SetOtherDestination(string dest)
        {
            SetSetting(KeyOtherDest, dest);
        }

        /// <summary>
        /// Get the appropriate StorageFolder for a file based on its MIME type.
        /// </summary>
        public static async Task<StorageFolder> GetFolderForFileAsync(string mimeType)
        {
            string category = GetCategory(mimeType);

            try
            {
                switch (category)
                {
                    case "image":
                        if (GetImageDestination() == DestLibrary)
                        {
                            var pics = KnownFolders.PicturesLibrary;
                            return await pics.CreateFolderAsync("LocalSend",
                                CreationCollisionOption.OpenIfExists);
                        }
                        break;

                    case "video":
                        if (GetVideoDestination() == DestLibrary)
                        {
                            var vids = KnownFolders.VideosLibrary;
                            return await vids.CreateFolderAsync("LocalSend",
                                CreationCollisionOption.OpenIfExists);
                        }
                        break;

                    case "audio":
                        if (GetAudioDestination() == DestLibrary)
                        {
                            var music = KnownFolders.MusicLibrary;
                            return await music.CreateFolderAsync("LocalSend",
                                CreationCollisionOption.OpenIfExists);
                        }
                        break;

                    case "other":
                        string otherDest = GetOtherDestination();
                        if (otherDest == DestSdCard)
                        {
                            var sdFolder = await GetSdCardFolderAsync();
                            if (sdFolder != null) return sdFolder;
                            // SD not available, fall through to fallback
                        }
                        else if (otherDest == DestLibrary)
                        {
                            return await GetInteropOtherFolderAsync();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Library/interop access failed for " + category + ": " + ex.Message);
            }

            // Fallback: app local folder (always works, even without interop)
            var local = ApplicationData.Current.LocalFolder;
            return await local.CreateFolderAsync("LocalSend",
                CreationCollisionOption.OpenIfExists);
        }

        /// <summary>
        /// Check if SD card (D:\) is accessible via interop. Result is cached.
        /// </summary>
        public static async Task<bool> IsSdCardAvailableAsync()
        {
            if (_sdChecked) return _sdAvailable;
            try
            {
                await StorageFolder.GetFolderFromPathAsync(@"D:\");
                _sdAvailable = true;
                AppLog.I(TAG, "SD card detected at D:\\");
            }
            catch
            {
                _sdAvailable = false;
                AppLog.I(TAG, "No SD card at D:\\");
            }
            _sdChecked = true;
            return _sdAvailable;
        }

        /// <summary>
        /// Get D:\LocalSend folder (SD card). Returns null if not available.
        /// </summary>
        private static async Task<StorageFolder> GetSdCardFolderAsync()
        {
            try
            {
                var root = await StorageFolder.GetFolderFromPathAsync(@"D:\");
                return await root.CreateFolderAsync("LocalSend",
                    CreationCollisionOption.OpenIfExists);
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "SD card folder failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Try to create a LocalSend folder on user-visible storage via interop.
        /// Tries C:\Data\Users\Public\LocalSend first (next to Pictures/Videos/Music),
        /// then D:\LocalSend (SD card). Falls back to LocalFolder if no interop.
        /// </summary>
        private static async Task<StorageFolder> GetInteropOtherFolderAsync()
        {
            foreach (var rootPath in InteropRoots)
            {
                try
                {
                    AppLog.I(TAG, "Trying interop root: " + rootPath);
                    var root = await StorageFolder.GetFolderFromPathAsync(rootPath);
                    var folder = await root.CreateFolderAsync("LocalSend",
                        CreationCollisionOption.OpenIfExists);
                    AppLog.I(TAG, "Interop OK: " + folder.Path);
                    return folder;
                }
                catch (Exception ex)
                {
                    AppLog.W(TAG, "Interop root failed: " + rootPath + " — " + ex.Message);
                }
            }

            AppLog.W(TAG, "All interop roots failed, falling back to LocalFolder");
            var local = ApplicationData.Current.LocalFolder;
            return await local.CreateFolderAsync("LocalSend",
                CreationCollisionOption.OpenIfExists);
        }

        /// <summary>
        /// Get human-readable destination label for a category (button text).
        /// </summary>
        public static string GetDestinationLabel(string category)
        {
            switch (category)
            {
                case "image":
                    return GetImageDestination() == DestLibrary ? S.PhotosLibrary : S.AppOnly;
                case "video":
                    return GetVideoDestination() == DestLibrary ? S.VideosLibrary : S.AppOnly;
                case "audio":
                    return GetAudioDestination() == DestLibrary ? S.MusicLibrary : S.AppOnly;
                default:
                    string dest = GetOtherDestination();
                    if (dest == DestSdCard) return S.SdCard;
                    if (dest == DestLibrary) return S.UserStorage;
                    return S.AppOnly;
            }
        }

        /// <summary>
        /// Get the filesystem path hint for a category (subtitle text).
        /// </summary>
        public static string GetDestinationPath(string category)
        {
            switch (category)
            {
                case "image":
                    return GetImageDestination() == DestLibrary ? S.PathPictures : S.PathAppOnly;
                case "video":
                    return GetVideoDestination() == DestLibrary ? S.PathVideos : S.PathAppOnly;
                case "audio":
                    return GetAudioDestination() == DestLibrary ? S.PathMusic : S.PathAppOnly;
                default:
                    string dest = GetOtherDestination();
                    if (dest == DestSdCard) return S.PathSdCard;
                    if (dest == DestLibrary) return S.PathUserStorage;
                    return S.PathAppOnly;
            }
        }

        /// <summary>
        /// Cycle "other" destination: library → sd (if available) → local → library...
        /// </summary>
        public static string NextOtherDestination(bool sdAvailable)
        {
            string current = GetOtherDestination();
            if (current == DestLibrary)
                return sdAvailable ? DestSdCard : DestLocal;
            if (current == DestSdCard)
                return DestLocal;
            return DestLibrary; // local → library
        }

        public static string GetCategory(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType)) return "other";
            if (mimeType.StartsWith("image/")) return "image";
            if (mimeType.StartsWith("video/")) return "video";
            if (mimeType.StartsWith("audio/")) return "audio";
            return "other";
        }

        // ══════════════════════════════════════
        // Migration: LocalFolder\LocalSend → user-visible LocalSend
        // ══════════════════════════════════════

        /// <summary>
        /// Returns the number of files in the old LocalFolder\LocalSend directory.
        /// Returns 0 if the folder doesn't exist or is empty.
        /// </summary>
        public static async Task<int> CountLegacyFilesAsync()
        {
            try
            {
                var folder = await TryGetLocalSendFolderAsync();
                if (folder == null)
                {
                    AppLog.V(TAG, "Legacy check: no LocalSend folder");
                    return 0;
                }
                AppLog.I(TAG, "Legacy folder found: " + folder.Path);
                var files = await folder.GetFilesAsync();
                AppLog.I(TAG, "Legacy files count: " + files.Count);
                foreach (var f in files)
                {
                    var props = await f.GetBasicPropertiesAsync();
                    AppLog.I(TAG, "  Legacy file: " + f.Name + " (" + props.Size + " bytes) path=" + f.Path);
                }
                return files.Count;
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Legacy check failed: " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Moves all files from LocalFolder\LocalSend to user-visible LocalSend folder.
        /// Returns (moved, failed) counts. Requires interop.
        /// </summary>
        public static async Task<MigrationResult> MigrateFilesAsync(IProgress<int> progress)
        {
            var result = new MigrationResult();

            StorageFolder oldFolder;
            try
            {
                oldFolder = await TryGetLocalSendFolderAsync();
                if (oldFolder == null)
                    return result;

                AppLog.I(TAG, "Migrate: old folder=" + oldFolder.Path);
            }
            catch (Exception ex)
            {
                AppLog.I(TAG, "Migrate: no old folder — " + ex.Message);
                return result; // no old folder
            }

            // Try to get/create LocalSend folder via interop
            StorageFolder newFolder = null;
            foreach (var rootPath in InteropRoots)
            {
                try
                {
                    AppLog.I(TAG, "Migrate: trying root " + rootPath);
                    var root = await StorageFolder.GetFolderFromPathAsync(rootPath);
                    newFolder = await root.CreateFolderAsync("LocalSend",
                        CreationCollisionOption.OpenIfExists);
                    AppLog.I(TAG, "Migrate: new folder=" + newFolder.Path);
                    break;
                }
                catch (Exception ex)
                {
                    AppLog.W(TAG, "Migrate: root failed " + rootPath + " — " + ex.Message);
                }
            }
            if (newFolder == null)
            {
                AppLog.W(TAG, "Migrate: all interop roots failed");
                result.InteropFailed = true;
                return result;
            }

            var files = await oldFolder.GetFilesAsync();
            result.Total = files.Count;
            AppLog.I(TAG, "Migrate: " + files.Count + " files to move");

            for (int i = 0; i < files.Count; i++)
            {
                try
                {
                    AppLog.I(TAG, "Migrate: moving " + files[i].Name + " → " + newFolder.Path);
                    await files[i].MoveAsync(newFolder, files[i].Name,
                        NameCollisionOption.GenerateUniqueName);
                    result.Moved++;
                    AppLog.I(TAG, "Migrate: OK " + files[i].Name);
                }
                catch (Exception ex)
                {
                    AppLog.W(TAG, "Migrate: FAILED " + files[i].Name + ": " + ex.Message);
                    result.Failed++;
                }

                if (progress != null)
                    progress.Report(i + 1);
            }

            // Clean up empty old folder
            try
            {
                var remaining = await oldFolder.GetFilesAsync();
                if (remaining.Count == 0)
                    await oldFolder.DeleteAsync();
            }
            catch { }

            return result;
        }

        private static async Task<StorageFolder> TryGetLocalSendFolderAsync()
        {
            var local = ApplicationData.Current.LocalFolder;
            var folders = await local.GetFoldersAsync();
            foreach (var folder in folders)
            {
                if (folder != null &&
                    string.Equals(folder.Name, "LocalSend", StringComparison.OrdinalIgnoreCase))
                {
                    return folder;
                }
            }

            return null;
        }

        public class MigrationResult
        {
            public int Total;
            public int Moved;
            public int Failed;
            public bool InteropFailed;
        }

        private static string GetSetting(string key, string defaultValue)
        {
            object val;
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out val) && val is string)
                return (string)val;
            return defaultValue;
        }

        private static void SetSetting(string key, string value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
    }
}
