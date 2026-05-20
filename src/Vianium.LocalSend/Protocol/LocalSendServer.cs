// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Vianium.LocalSend.Data;
using Vianium.LocalSend.Discovery;
using Vianium.LocalSend.Json;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Server;

namespace Vianium.LocalSend.Protocol
{
    internal sealed class LocalSendServer : IDisposable
    {
        private readonly HttpServer _httpServer;
        private readonly DeviceInfo _localDevice;
        private readonly DeviceTracker _tracker;

        private TransferSession _activeSession;
        private TaskCompletionSource<bool> _approvalTcs;
        private readonly SemaphoreSlim _sessionLock = new SemaphoreSlim(1, 1);

        private const string TAG = "Server";

        public event Action<TransferSession> IncomingTransferRequested;
        public event Action<string, long, long> FileReceiveProgress;
        public event Action<TransferSession> TransferCompleted;

        public LocalSendServer(DeviceInfo localDevice, DeviceTracker tracker)
        {
            _localDevice = localDevice;
            _tracker = tracker;
            _httpServer = new HttpServer();
            _httpServer.UnknownConnectionReceived += OnUnknownConnection;
            RegisterRoutes();
        }

        private void OnUnknownConnection(string remoteIp)
        {
            AppLog.I(TAG, "HTTPS client detected at " + remoteIp + " — reverse discovering");
            ReverseDiscoverAsync(remoteIp);
        }

        private void RegisterRoutes()
        {
            string prefix = ProtocolConstants.ApiPrefix;
            string prefixV1 = ProtocolConstants.ApiPrefixV1;

            // v2 routes
            _httpServer.AddRoute("GET", prefix + "/info", HandleInfo);
            _httpServer.AddRoute("POST", prefix + "/register", HandleRegister);
            _httpServer.AddRoute("POST", prefix + "/prepare-upload", HandlePrepareUpload);
            _httpServer.AddRoute("POST", prefix + "/cancel", HandleCancel);

            // v1 compatibility routes
            _httpServer.AddRoute("POST", prefixV1 + "/info", HandleInfo);
            _httpServer.AddRoute("POST", prefixV1 + "/register", HandleRegister);
            _httpServer.AddRoute("POST", prefixV1 + "/send-request", HandlePrepareUpload);
            _httpServer.AddRoute("POST", prefixV1 + "/cancel", HandleCancel);

            // Streaming routes for file upload (body streamed to disk, not buffered)
            _httpServer.AddStreamingRoute("POST", prefix + "/upload", HandleUploadStreaming);
            _httpServer.AddStreamingRoute("POST", prefixV1 + "/send", HandleUploadStreaming);
        }

        public async Task StartAsync()
        {
            await _httpServer.StartAsync(ProtocolConstants.Port);
        }

        public void Stop()
        {
            _httpServer.Stop();
            CancelActiveSession();
        }

        #region Route Handlers

        private Task<ServerResponse> HandleInfo(ServerRequest req)
        {
            string fingerprint = req.GetQueryParam("fingerprint");
            if (fingerprint != null && fingerprint == _localDevice.Fingerprint)
                return Task.FromResult(ServerResponse.PreconditionFailed());

            AppLog.I(TAG, "Info request from " + req.RemoteAddress +
                " fingerprint=" + (fingerprint ?? "none"));

            // Reverse-discover the requesting device (async, fire-and-forget)
            if (req.RemoteAddress != null)
            {
                ReverseDiscoverAsync(req.RemoteAddress);
            }

            var json = _localDevice.ToInfoJson();
            return Task.FromResult(ServerResponse.Ok(json));
        }

        /// <summary>
        /// When a device queries our /info, try to get THEIR info back.
        /// This enables bidirectional discovery even without multicast.
        /// </summary>
        private async void ReverseDiscoverAsync(string remoteIp)
        {
            try
            {
                // Check if we already know this device
                var existing = _tracker.GetAll();
                foreach (var d in existing)
                {
                    if (d.IpAddress == remoteIp) return;
                }

                AppLog.V(TAG, "Reverse-discovering " + remoteIp);

                // Try HTTPS first, then HTTP
                var client = new Network.PlainHttpClient();
                string path = ProtocolConstants.ApiPrefix + "/info?fingerprint=" +
                    Uri.EscapeDataString(_localDevice.Fingerprint);

                Network.PlainHttpResponse response = null;

                // Try HTTPS first
                try
                {
                    client.UseSsl = true;
                    response = await client.GetAsync(remoteIp, ProtocolConstants.Port, path,
                        System.Threading.CancellationToken.None);
                    AppLog.V(TAG, "Reverse HTTPS response from " + remoteIp +
                        ": status=" + response.StatusCode +
                        " bodyLen=" + (response.Body != null ? response.Body.Length : 0));
                }
                catch (Exception ex)
                {
                    AppLog.V(TAG, "Reverse HTTPS failed for " + remoteIp + ": " + ex.Message);
                    // Try HTTP fallback
                    try
                    {
                        client.UseSsl = false;
                        response = await client.GetAsync(remoteIp, ProtocolConstants.Port, path,
                            System.Threading.CancellationToken.None);
                        AppLog.V(TAG, "Reverse HTTP response from " + remoteIp +
                            ": status=" + response.StatusCode);
                    }
                    catch (Exception ex2)
                    {
                        AppLog.V(TAG, "Reverse HTTP also failed for " + remoteIp + ": " + ex2.Message);
                    }
                }

                if (response == null)
                {
                    AppLog.V(TAG, "Reverse-discover: no response from " + remoteIp);
                    return;
                }

                AppLog.V(TAG, "Reverse-discover response: status=" + response.StatusCode +
                    " body=" + (response.GetBodyAsString().Length > 200
                        ? response.GetBodyAsString().Substring(0, 200) + "..."
                        : response.GetBodyAsString()));

                if (response.StatusCode == 200)
                {
                    Json.JsonValue json;
                    string bodyStr = response.GetBodyAsString();
                    if (Json.SimpleJsonParser.TryParse(bodyStr, out json))
                    {
                        var info = DeviceInfo.FromJson(json);
                        if (info != null && info.Fingerprint != _localDevice.Fingerprint)
                        {
                            AppLog.I(TAG, "Reverse-discovered: " + info.Alias + " at " + remoteIp +
                                " type=" + info.DeviceType + " proto=" + info.Protocol);
                            _tracker.AddOrUpdate(info, remoteIp);
                        }
                    }
                    else
                    {
                        AppLog.W(TAG, "Reverse-discover: invalid JSON from " + remoteIp + ": " + bodyStr);
                    }
                }
                else
                {
                    AppLog.V(TAG, "Reverse-discover: non-200 status " + response.StatusCode + " from " + remoteIp);
                }
            }
            catch (Exception ex)
            {
                AppLog.V(TAG, "Reverse-discover failed for " + remoteIp + ": " + ex.Message);
            }
        }

        private Task<ServerResponse> HandleRegister(ServerRequest req)
        {
            try
            {
                string body = req.GetBodyAsString();
                if (string.IsNullOrEmpty(body))
                {
                    var responseJson = _localDevice.ToRegisterJson();
                    return Task.FromResult(ServerResponse.Ok(responseJson));
                }

                JsonValue json;
                if (!SimpleJsonParser.TryParse(body, out json))
                    return Task.FromResult(ServerResponse.BadRequest("Invalid JSON"));

                var deviceInfo = DeviceInfo.FromJson(json);
                if (deviceInfo != null && deviceInfo.Fingerprint != _localDevice.Fingerprint)
                    _tracker.AddOrUpdate(deviceInfo, req.RemoteAddress);

                var respJson = _localDevice.ToRegisterJson();
                return Task.FromResult(ServerResponse.Ok(respJson));
            }
            catch (Exception ex)
            {
                AppLog.I(TAG, "Register error: " + ex.Message);
                return Task.FromResult(ServerResponse.BadRequest("Invalid register request"));
            }
        }

        private async Task<ServerResponse> HandlePrepareUpload(ServerRequest req)
        {
            await _sessionLock.WaitAsync();
            try
            {
                if (_activeSession != null && _activeSession.State == TransferState.Transferring)
                    return ServerResponse.Conflict("Blocked by another session");

                string body = req.GetBodyAsString();
                AppLog.V(TAG, "PrepareUpload body length=" + (body != null ? body.Length : 0));
                AppLog.V(TAG, "PrepareUpload body: " + (body != null && body.Length > 500 ? body.Substring(0, 500) + "..." : body));

                JsonValue json;
                if (!SimpleJsonParser.TryParse(body, out json) || !json.IsObject)
                {
                    AppLog.W(TAG, "PrepareUpload: invalid JSON body");
                    return ServerResponse.BadRequest("Invalid request body");
                }

                // Parse sender info
                DeviceInfo senderInfo = null;
                if (json.ContainsKey("info"))
                    senderInfo = DeviceInfo.FromJson(json["info"]);
                if (senderInfo == null)
                    senderInfo = new DeviceInfo { Alias = "Unknown", Fingerprint = "" };

                AppLog.I(TAG, "PrepareUpload from: " + senderInfo.Alias +
                    " (" + senderInfo.DeviceModel + ") fingerprint=" + senderInfo.Fingerprint);

                if (senderInfo.Fingerprint == _localDevice.Fingerprint)
                {
                    AppLog.W(TAG, "PrepareUpload: self-discovery, returning 412");
                    return ServerResponse.PreconditionFailed();
                }

                // Parse files
                if (!json.ContainsKey("files") || !json["files"].IsObject)
                {
                    AppLog.W(TAG, "PrepareUpload: missing files object");
                    return ServerResponse.BadRequest("Missing files");
                }

                var filesJson = json["files"];
                var session = new TransferSession();
                session.RemoteDevice = senderInfo;

                bool hasTextOnly = true;

                foreach (string fileId in filesJson.Keys)
                {
                    var fileDto = FileDto.FromJson(fileId, filesJson[fileId]);
                    if (fileDto != null)
                    {
                        string token = TokenGenerator.Generate();
                        session.Files[fileId] = new FileTransferInfo(fileDto, token);

                        AppLog.I(TAG, "  File: id=" + fileId +
                            " name=" + fileDto.FileName +
                            " size=" + fileDto.Size +
                            " type=" + fileDto.FileType);

                        if (fileDto.Size > 0)
                            hasTextOnly = false;
                    }
                }

                AppLog.I(TAG, "PrepareUpload: " + session.Files.Count + " files, textOnly=" + hasTextOnly);

                if (session.Files.Count == 0)
                {
                    AppLog.W(TAG, "PrepareUpload: no valid files parsed");
                    return ServerResponse.BadRequest("No valid files");
                }

                // Text-only message: save as text file, auto-accept, return 204
                if (hasTextOnly)
                {
                    foreach (var kvp in session.Files)
                    {
                        if (kvp.Value.File.Preview != null)
                        {
                            await SaveTextMessageAsync(kvp.Value.File.Preview, senderInfo.Alias);
                            kvp.Value.Status = FileTransferStatus.Completed;
                        }
                    }
                    session.State = TransferState.Completed;
                    var completedHandler = TransferCompleted;
                    if (completedHandler != null)
                        completedHandler(session);
                    return ServerResponse.NoContent();
                }

                _tracker.AddOrUpdate(senderInfo, req.RemoteAddress);

                _activeSession = session;
                _approvalTcs = new TaskCompletionSource<bool>();

                var handler = IncomingTransferRequested;
                if (handler != null)
                    handler(session);

                // Wait for user approval (60 second timeout)
                bool approved;
                try
                {
                    var completedTask = await Task.WhenAny(_approvalTcs.Task, Task.Delay(60000));
                    approved = completedTask == _approvalTcs.Task && _approvalTcs.Task.Result;

                    if (completedTask != _approvalTcs.Task)
                    {
                        _activeSession = null;
                        return ServerResponse.Forbidden("Request timed out");
                    }
                }
                catch
                {
                    _activeSession = null;
                    return ServerResponse.Forbidden("Request cancelled");
                }

                if (!approved)
                {
                    _activeSession = null;
                    return ServerResponse.Forbidden("Request declined");
                }

                session.State = TransferState.Transferring;

                var responseJson = new JsonObject();
                responseJson.Set("sessionId", new JsonString(session.SessionId));

                var filesResponseJson = new JsonObject();
                foreach (var kvp in session.Files)
                {
                    if (kvp.Value.Status != FileTransferStatus.Skipped)
                        filesResponseJson.Set(kvp.Key, new JsonString(kvp.Value.Token));
                }
                responseJson.Set("files", filesResponseJson);

                return ServerResponse.Ok(responseJson);
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "PrepareUpload error", ex);
                _activeSession = null;
                return ServerResponse.InternalError(ex.Message);
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        /// <summary>
        /// Streaming upload handler — reads body in 8KB chunks directly to disk.
        /// Supports files of any size without running out of memory.
        /// </summary>
        private async Task<ServerResponse> HandleUploadStreaming(
            ServerRequest req, DataReader bodyReader, int contentLength)
        {
            string sessionId = req.GetQueryParam("sessionId");
            string fileId = req.GetQueryParam("fileId");
            string token = req.GetQueryParam("token");

            AppLog.I(TAG, "Upload: sessionId=" + sessionId + " fileId=" + fileId +
                " token=" + (token != null ? token.Substring(0, Math.Min(8, token.Length)) + "..." : "null") +
                " contentLength=" + contentLength);

            if (string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(token))
            {
                AppLog.W(TAG, "Upload: missing fileId or token");
                return ServerResponse.BadRequest("Missing fileId or token");
            }

            if (_activeSession == null)
            {
                AppLog.W(TAG, "Upload: no active session");
                return ServerResponse.Conflict("No active session");
            }

            AppLog.V(TAG, "Upload: active session=" + _activeSession.SessionId +
                " state=" + _activeSession.State + " files=" + _activeSession.Files.Count);

            if (sessionId != null && _activeSession.SessionId != sessionId)
            {
                AppLog.W(TAG, "Upload: session mismatch. Expected=" + _activeSession.SessionId + " got=" + sessionId);
                return ServerResponse.Conflict("Session mismatch");
            }

            FileTransferInfo fileInfo;
            if (!_activeSession.Files.TryGetValue(fileId, out fileInfo))
                return ServerResponse.BadRequest("Unknown fileId");

            if (fileInfo.Token != token)
                return ServerResponse.Forbidden("Invalid token");

            try
            {
                fileInfo.Status = FileTransferStatus.Transferring;

                string safeName = SanitizeFileName(fileInfo.File.FileName);
                var receivedFolder = await StorageConfig.GetFolderForFileAsync(
                    fileInfo.File.FileType);
                var file = await receivedFolder.CreateFileAsync(safeName,
                    CreationCollisionOption.GenerateUniqueName);

                // Stream body directly to file in 8KB chunks
                long totalWritten = 0;
                using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                using (var fileWriter = new DataWriter(fileStream))
                {
                    int remaining = contentLength;
                    while (remaining > 0)
                    {
                        uint toRead = (uint)Math.Min(remaining, 8192);
                        uint loaded = await bodyReader.LoadAsync(toRead);
                        if (loaded == 0)
                            throw new Exception("Connection closed during upload");

                        byte[] chunk = new byte[loaded];
                        bodyReader.ReadBytes(chunk);
                        fileWriter.WriteBytes(chunk);

                        totalWritten += loaded;
                        remaining -= (int)loaded;

                        // Report progress
                        fileInfo.BytesTransferred = totalWritten;
                        var progressHandler = FileReceiveProgress;
                        if (progressHandler != null)
                            progressHandler(fileId, totalWritten, fileInfo.File.Size);
                    }
                    await fileWriter.StoreAsync();
                    await fileWriter.FlushAsync();
                }

                fileInfo.Status = FileTransferStatus.Completed;
                fileInfo.SavedPath = file.Path;

                AppLog.I(TAG, "Received file: " + safeName + " (" + totalWritten + " bytes, streamed)");

                if (_activeSession.AllFilesCompleted)
                {
                    _activeSession.State = _activeSession.HasErrors
                        ? TransferState.Failed
                        : TransferState.Completed;
                    var completedHandler = TransferCompleted;
                    if (completedHandler != null)
                        completedHandler(_activeSession);
                }

                return ServerResponse.Ok(new JsonObject());
            }
            catch (Exception ex)
            {
                fileInfo.Status = FileTransferStatus.Failed;
                fileInfo.ErrorMessage = ex.Message;
                AppLog.I(TAG, "Upload error for " + fileInfo.File.FileName + ": " + ex.Message);
                return ServerResponse.InternalError(ex.Message);
            }
        }

        private Task<ServerResponse> HandleCancel(ServerRequest req)
        {
            string sessionId = req.GetQueryParam("sessionId");

            if (_activeSession != null &&
                (sessionId == null || _activeSession.SessionId == sessionId))
            {
                CancelActiveSession();
            }

            return Task.FromResult(ServerResponse.Ok(new JsonObject()));
        }

        #endregion

        #region Text Messages

        private async Task SaveTextMessageAsync(string text, string senderAlias)
        {
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var receivedFolder = await folder.CreateFolderAsync("LocalSend",
                    CreationCollisionOption.OpenIfExists);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = "text_" + timestamp + ".txt";
                var file = await receivedFolder.CreateFileAsync(fileName,
                    CreationCollisionOption.GenerateUniqueName);

                string content = "From: " + senderAlias + "\n"
                    + "Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n"
                    + text;

                await FileIO.WriteTextAsync(file, content);
                AppLog.I(TAG, "Received text message from " + senderAlias);
            }
            catch (Exception ex)
            {
                AppLog.I(TAG, "Failed to save text message: " + ex.Message);
            }
        }

        #endregion

        #region Session Management

        public void AcceptTransfer()
        {
            if (_approvalTcs != null)
                _approvalTcs.TrySetResult(true);
        }

        public void DeclineTransfer()
        {
            if (_approvalTcs != null)
                _approvalTcs.TrySetResult(false);
        }

        private void CancelActiveSession()
        {
            if (_activeSession != null)
            {
                _activeSession.State = TransferState.Cancelled;
                _activeSession = null;
            }
            if (_approvalTcs != null)
            {
                _approvalTcs.TrySetResult(false);
                _approvalTcs = null;
            }
        }

        public TransferSession ActiveSession { get { return _activeSession; } }

        #endregion

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "unnamed_file";

            int lastSlash = fileName.LastIndexOf('/');
            int lastBackslash = fileName.LastIndexOf('\\');
            int lastSep = Math.Max(lastSlash, lastBackslash);
            if (lastSep >= 0 && lastSep < fileName.Length - 1)
                fileName = fileName.Substring(lastSep + 1);

            char[] invalid = { '<', '>', ':', '"', '|', '?', '*' };
            for (int i = 0; i < invalid.Length; i++)
                fileName = fileName.Replace(invalid[i], '_');

            if (string.IsNullOrEmpty(fileName)) return "unnamed_file";
            return fileName;
        }

        public void Dispose()
        {
            Stop();
            _sessionLock.Dispose();
        }
    }
}
