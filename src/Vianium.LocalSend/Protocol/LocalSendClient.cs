// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Vianium.LocalSend.Discovery;
using Vianium.LocalSend.Json;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Network;

namespace Vianium.LocalSend.Protocol
{
    internal sealed class LocalSendClient
    {
        private const string TAG = "Client";
        private readonly PlainHttpClient _http;
        private readonly DeviceInfo _localDevice;

        public LocalSendClient(DeviceInfo localDevice)
        {
            _localDevice = localDevice;
            _http = new PlainHttpClient();
        }


        public async Task<DeviceInfo> GetInfoAsync(string host, int port, CancellationToken ct)
        {
            AppLog.D(TAG, "GetInfo from " + host + ":" + port);

            string path = ProtocolConstants.ApiPrefix + "/info?fingerprint=" +
                          Uri.EscapeDataString(_localDevice.Fingerprint);

            var response = await _http.GetAsync(host, port, path, ct);

            if (response.StatusCode == 412)
            {
                AppLog.V(TAG, "GetInfo: 412 from " + host + " — that device is us");
                return null;
            }

            if (response.StatusCode != 200)
            {
                AppLog.W(TAG, "GetInfo from " + host + " failed: status " + response.StatusCode);
                throw new Exception("Info request failed with status " + response.StatusCode);
            }

            JsonValue json;
            if (!SimpleJsonParser.TryParse(response.GetBodyAsString(), out json))
            {
                AppLog.W(TAG, "GetInfo: invalid JSON from " + host);
                return null;
            }
            return DeviceInfo.FromJson(json);
        }

        public async Task<TransferSession> PrepareUploadAsync(string host, int port,
            List<FileDto> files, CancellationToken ct)
        {
            var requestJson = new JsonObject();
            requestJson.Set("info", _localDevice.ToRegisterJson());

            var filesJson = new JsonObject();
            foreach (var file in files)
                filesJson.Set(file.Id, file.ToJson());
            requestJson.Set("files", filesJson);

            string body = SimpleJsonParser.Serialize(requestJson);
            string path = ProtocolConstants.ApiPrefix + "/prepare-upload";

            AppLog.I(TAG, "Sending prepare-upload to " + host + " with " + files.Count + " files");

            var response = await _http.PostJsonAsync(host, port, path, body, ct);

            switch (response.StatusCode)
            {
                case 200:
                    break;
                case 204:
                    return null;
                case 403:
                    throw new Exception("Transfer declined by receiver");
                case 409:
                    throw new Exception("Receiver is busy with another transfer");
                case 401:
                    throw new Exception("PIN required (not supported)");
                case 429:
                    throw new Exception("Too many attempts");
                default:
                    throw new Exception("Prepare-upload failed with status " + response.StatusCode);
            }

            JsonValue responseJson;
            string responseBody = response.GetBodyAsString();
            if (!SimpleJsonParser.TryParse(responseBody, out responseJson))
            {
                AppLog.W(TAG, "PrepareUpload: invalid JSON response: " + responseBody);
                throw new Exception("Invalid JSON in prepare-upload response");
            }

            if (!responseJson.IsObject || !responseJson.ContainsKey("sessionId") ||
                !responseJson.ContainsKey("files"))
            {
                throw new Exception("Invalid prepare-upload response");
            }

            string sessionId = responseJson["sessionId"].AsString();
            var acceptedFiles = responseJson["files"];

            var session = new TransferSession();
            session.SessionId = sessionId;
            session.State = TransferState.Accepted;

            foreach (var file in files)
            {
                if (acceptedFiles.ContainsKey(file.Id))
                {
                    string token = acceptedFiles[file.Id].AsString();
                    session.Files[file.Id] = new FileTransferInfo(file, token);
                }
            }

            AppLog.I(TAG, "Upload prepared: sessionId=" + sessionId +
                  ", accepted " + session.Files.Count + "/" + files.Count + " files");

            return session;
        }

        /// <summary>
        /// Upload a file by streaming directly from StorageFile (no full memory buffer).
        /// </summary>
        public async Task UploadFileStreamAsync(string host, int port, string sessionId,
            string fileId, string token, StorageFile file, long fileSize, string contentType,
            IProgress<long> progress, CancellationToken ct)
        {
            string queryString = "sessionId=" + Uri.EscapeDataString(sessionId) +
                                 "&fileId=" + Uri.EscapeDataString(fileId) +
                                 "&token=" + Uri.EscapeDataString(token);

            string path = ProtocolConstants.ApiPrefix + "/upload";

            AppLog.I(TAG, "Uploading file " + fileId + " (" + fileSize + " bytes, streaming)");

            var response = await _http.PostFileStreamAsync(host, port, path,
                queryString, file, fileSize, contentType ?? "application/octet-stream",
                progress, ct);

            if (response.StatusCode != 200)
                throw new Exception("Upload failed with status " + response.StatusCode);

            AppLog.I(TAG, "File " + fileId + " uploaded successfully");
        }

        /// <summary>
        /// Fallback: upload from byte array (for small files or text messages).
        /// </summary>
        public async Task UploadFileAsync(string host, int port, string sessionId,
            string fileId, string token, byte[] fileBytes, string contentType,
            IProgress<long> progress, CancellationToken ct)
        {
            string queryString = "sessionId=" + Uri.EscapeDataString(sessionId) +
                                 "&fileId=" + Uri.EscapeDataString(fileId) +
                                 "&token=" + Uri.EscapeDataString(token);

            string path = ProtocolConstants.ApiPrefix + "/upload";

            var response = await _http.PostBinaryAsync(host, port, path,
                queryString, fileBytes, contentType ?? "application/octet-stream", progress, ct);

            if (response.StatusCode != 200)
                throw new Exception("Upload failed with status " + response.StatusCode);
        }

        public async Task CancelAsync(string host, int port, string sessionId, CancellationToken ct)
        {
            AppLog.I(TAG, "Cancelling session " + sessionId + " on " + host);
            try
            {
                string path = ProtocolConstants.ApiPrefix + "/cancel?sessionId=" +
                              Uri.EscapeDataString(sessionId);
                await _http.PostJsonAsync(host, port, path, "{}", ct);
            }
            catch (Exception ex)
            {
                AppLog.I(TAG, "Cancel error: " + ex.Message);
            }
        }

        /// <summary>
        /// Send text message to a device (uses preview field, no actual file upload).
        /// </summary>
        public async Task SendTextAsync(DiscoveredDevice target, string text, CancellationToken ct)
        {
            AppLog.I(TAG, "Sending text message (" + (text != null ? text.Length : 0) +
                " chars) to " + target.Alias + " at " + target.IpAddress);

            string fileId = Guid.NewGuid().ToString();
            var fileDto = new FileDto
            {
                Id = fileId,
                FileName = "text",
                Size = 0,
                FileType = "text/plain",
                Preview = text
            };

            var requestJson = new JsonObject();
            requestJson.Set("info", _localDevice.ToRegisterJson());

            var filesJson = new JsonObject();
            filesJson.Set(fileId, fileDto.ToJson());
            requestJson.Set("files", filesJson);

            string body = SimpleJsonParser.Serialize(requestJson);
            string path = ProtocolConstants.ApiPrefix + "/prepare-upload";

            _http.UseSsl = target.Info != null && target.Info.Protocol == "https";
            // target.Info can be null for a device discovered only via a raw
            // connection — fall back to the default port instead of an NRE.
            int port = target.Info != null ? target.Info.Port : ProtocolConstants.Port;
            var response = await _http.PostJsonAsync(target.IpAddress, port, path, body, ct);

            if (response.StatusCode == 204)
            {
                AppLog.I(TAG, "Text message sent successfully");
                return;
            }

            if (response.StatusCode != 200)
                throw new Exception("Text send failed with status " + response.StatusCode);
        }

        /// <summary>
        /// Orchestrates sending files to a target device using streaming.
        /// </summary>
        public async Task SendFilesAsync(DiscoveredDevice target, List<FileDto> files,
            Dictionary<string, StorageFile> storageFiles, IProgress<TransferProgress> progress,
            CancellationToken ct)
        {
            string host = target.IpAddress;
            int port = target.Info != null ? target.Info.Port : ProtocolConstants.Port;

            // Use HTTPS if target device requires it
            _http.UseSsl = target.Info != null && target.Info.Protocol == "https";
            AppLog.I(TAG, "SendFiles to " + host + ":" + port +
                " protocol=" + (target.Info != null ? target.Info.Protocol ?? "http" : "http") +
                " ssl=" + _http.UseSsl);

            // Retry prepare-upload up to 2 times (device might be briefly unavailable)
            TransferSession session = null;
            int maxRetries = 2;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    session = await PrepareUploadAsync(host, port, files, ct);
                    break;
                }
                catch (OperationCanceledException)
                {
                    // User cancelled — propagate immediately, do not retry.
                    throw;
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        AppLog.W(TAG, "PrepareUpload attempt " + (attempt + 1) +
                            " failed: " + ex.Message + " — retrying in 2s");
                        await Task.Delay(2000, ct);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            if (session == null)
            {
                AppLog.I(TAG, "Transfer was text-only or returned no content");
                return;
            }

            session.State = TransferState.Transferring;

            int completed = 0;
            int total = session.Files.Count;

            foreach (var kvp in session.Files)
            {
                ct.ThrowIfCancellationRequested();

                string fileId = kvp.Key;
                var fileInfo = kvp.Value;

                try
                {
                    StorageFile storageFile;
                    if (!storageFiles.TryGetValue(fileId, out storageFile))
                    {
                        fileInfo.Status = FileTransferStatus.Failed;
                        fileInfo.ErrorMessage = "File not available";
                        continue;
                    }

                    fileInfo.Status = FileTransferStatus.Transferring;

                    var fileProgress = new Progress<long>(bytes =>
                    {
                        fileInfo.BytesTransferred = bytes;
                        if (progress != null)
                        {
                            progress.Report(new TransferProgress
                            {
                                CurrentFileId = fileId,
                                CurrentFileBytes = bytes,
                                TotalFiles = total,
                                CompletedFiles = completed
                            });
                        }
                    });

                    await UploadFileStreamAsync(host, port, session.SessionId,
                        fileId, fileInfo.Token, storageFile, fileInfo.File.Size,
                        fileInfo.File.FileType, fileProgress, ct);

                    fileInfo.Status = FileTransferStatus.Completed;
                    fileInfo.BytesTransferred = fileInfo.File.Size;
                    completed++;
                }
                catch (Exception ex)
                {
                    fileInfo.Status = FileTransferStatus.Failed;
                    fileInfo.ErrorMessage = ex.Message;
                    AppLog.I(TAG, "Failed to upload " + fileInfo.File.FileName + ": " + ex.Message);
                }
            }

            session.State = session.HasErrors
                ? TransferState.Failed
                : TransferState.Completed;

            AppLog.I(TAG, "Transfer complete: " + completed + "/" + total + " files sent");
        }

        /// <summary>
        /// Legacy overload for byte[] data (kept for backward compat).
        /// </summary>
        public async Task SendFilesAsync(DiscoveredDevice target, List<FileDto> files,
            Dictionary<string, byte[]> fileData, IProgress<TransferProgress> progress,
            CancellationToken ct)
        {
            string host = target.IpAddress;
            int port = target.Info.Port;

            var session = await PrepareUploadAsync(host, port, files, ct);
            if (session == null) return;

            session.State = TransferState.Transferring;

            int completed = 0;
            int total = session.Files.Count;

            foreach (var kvp in session.Files)
            {
                ct.ThrowIfCancellationRequested();

                string fileId = kvp.Key;
                var fileInfo = kvp.Value;

                try
                {
                    byte[] data;
                    if (!fileData.TryGetValue(fileId, out data))
                    {
                        fileInfo.Status = FileTransferStatus.Failed;
                        fileInfo.ErrorMessage = "File data not available";
                        continue;
                    }

                    fileInfo.Status = FileTransferStatus.Transferring;

                    var fileProgress = new Progress<long>(bytes =>
                    {
                        fileInfo.BytesTransferred = bytes;
                        if (progress != null)
                        {
                            progress.Report(new TransferProgress
                            {
                                CurrentFileId = fileId,
                                CurrentFileBytes = bytes,
                                TotalFiles = total,
                                CompletedFiles = completed
                            });
                        }
                    });

                    await UploadFileAsync(host, port, session.SessionId,
                        fileId, fileInfo.Token, data, fileInfo.File.FileType,
                        fileProgress, ct);

                    fileInfo.Status = FileTransferStatus.Completed;
                    fileInfo.BytesTransferred = data.Length;
                    completed++;
                }
                catch (Exception ex)
                {
                    fileInfo.Status = FileTransferStatus.Failed;
                    fileInfo.ErrorMessage = ex.Message;
                    AppLog.I(TAG, "Failed to upload " + fileInfo.File.FileName + ": " + ex.Message);
                }
            }

            session.State = session.HasErrors ? TransferState.Failed : TransferState.Completed;
        }

    }

    internal sealed class TransferProgress
    {
        public string CurrentFileId { get; set; }
        public long CurrentFileBytes { get; set; }
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
    }
}
