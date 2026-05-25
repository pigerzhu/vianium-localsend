// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Vianium.LocalSend.Logging;

namespace Vianium.LocalSend.Server
{
    internal delegate Task<ServerResponse> StreamingRequestHandler(
        ServerRequest headersOnly, DataReader bodyReader, int contentLength);

    internal sealed class HttpServer : IDisposable
    {
        private const string TAG = "HttpServer";
        private StreamSocketListener _listener;
        private readonly Dictionary<string, Func<ServerRequest, Task<ServerResponse>>> _routes;
        private readonly Dictionary<string, StreamingRequestHandler> _streamingRoutes;
        private bool _running;

        /// <summary>
        /// Fired when a connection fails to parse (e.g., HTTPS client connecting to HTTP server).
        /// Provides the remote IP so the caller can attempt reverse discovery.
        /// </summary>
        public event Action<string> UnknownConnectionReceived;

        public HttpServer()
        {
            _routes = new Dictionary<string, Func<ServerRequest, Task<ServerResponse>>>(StringComparer.OrdinalIgnoreCase);
            _streamingRoutes = new Dictionary<string, StreamingRequestHandler>(StringComparer.OrdinalIgnoreCase);
        }

        public void AddRoute(string method, string path, Func<ServerRequest, Task<ServerResponse>> handler)
        {
            string key = method.ToUpperInvariant() + " " + path;
            _routes[key] = handler;
        }

        public void AddStreamingRoute(string method, string path, StreamingRequestHandler handler)
        {
            string key = method.ToUpperInvariant() + " " + path;
            _streamingRoutes[key] = handler;
        }

        public async Task StartAsync(int port)
        {
            if (_running) return;

            var listener = new StreamSocketListener();
            listener.ConnectionReceived += OnConnectionReceived;
            listener.Control.QualityOfService = SocketQualityOfService.LowLatency;

            try
            {
                await listener.BindServiceNameAsync(port.ToString());
                _listener = listener;
                _running = true;
            }
            catch
            {
                SafeDisposeListener(listener);
                throw;
            }

            AppLog.I(TAG, "Started on port " + port);
        }

        public void Stop()
        {
            if (!_running && _listener == null) return;
            _running = false;

            var listener = _listener;
            _listener = null;
            SafeDisposeListener(listener);

            AppLog.I(TAG, "Stopped");
        }

        private void SafeDisposeListener(StreamSocketListener listener)
        {
            if (listener == null) return;

            try
            {
                listener.ConnectionReceived -= OnConnectionReceived;
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Ignoring listener event detach error: " + ex.Message);
            }

            try
            {
                listener.Dispose();
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Ignoring listener dispose error: " + ex.Message);
            }
        }

        private async void OnConnectionReceived(StreamSocketListener sender,
            StreamSocketListenerConnectionReceivedEventArgs args)
        {
            if (!_running)
            {
                try { args.Socket.Dispose(); } catch { }
                return;
            }

            try
            {
                await HandleConnectionAsync(args.Socket);
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "Connection error", ex);
            }
            finally
            {
                try { args.Socket.Dispose(); } catch { }
            }
        }

        private async Task HandleConnectionAsync(StreamSocket socket)
        {
            string remoteAddress = socket.Information.RemoteAddress.DisplayName;

            using (var reader = new DataReader(socket.InputStream))
            using (var writer = new DataWriter(socket.OutputStream))
            {
                reader.InputStreamOptions = InputStreamOptions.Partial;

                ServerRequest request;
                try
                {
                    request = await RequestParser.ParseHeadersAsync(reader);
                    if (request == null)
                    {
                        // TLS client connecting to plain HTTP — trigger reverse discovery
                        AppLog.V(TAG, "Empty/TLS connection from " + remoteAddress);
                        NotifyUnknownConnection(remoteAddress);
                        return;
                    }
                    request.RemoteAddress = remoteAddress;
                }
                catch
                {
                    // HTTPS client sent TLS bytes that aren't valid HTTP
                    AppLog.V(TAG, "Non-HTTP connection from " + remoteAddress + " (likely HTTPS client)");
                    NotifyUnknownConnection(remoteAddress);
                    return;
                }

                AppLog.V(TAG, request.Method + " " + request.Path +
                    (request.QueryString != null ? "?" + request.QueryString : "") +
                    " from " + remoteAddress +
                    " Content-Length=" + request.ContentLength);

                string routeKey = request.Method + " " + request.Path;

                // Check streaming routes first (for /upload)
                StreamingRequestHandler streamingHandler;
                if (_streamingRoutes.TryGetValue(routeKey, out streamingHandler))
                {
                    AppLog.V(TAG, "Routing to streaming handler: " + routeKey);
                    ServerResponse response;
                    try
                    {
                        response = await streamingHandler(request, reader, request.ContentLength);
                    }
                    catch (Exception ex)
                    {
                        AppLog.E(TAG, "Streaming handler error for " + routeKey, ex);
                        response = ServerResponse.InternalError(ex.Message);
                    }
                    AppLog.V(TAG, "Response: " + response.StatusCode + " for " + routeKey);
                    await WriteResponseAsync(writer, response);
                    return;
                }

                // Regular routes — read full body
                if (request.ContentLength > 0)
                {
                    AppLog.V(TAG, "Reading body: " + request.ContentLength + " bytes");
                    request.Body = await RequestParser.ReadBodyAsync(reader, request.ContentLength);
                    AppLog.V(TAG, "Body read complete: " + request.Body.Length + " bytes");
                }

                Func<ServerRequest, Task<ServerResponse>> handler;
                if (!_routes.TryGetValue(routeKey, out handler))
                {
                    string altPath = request.Path.EndsWith("/")
                        ? request.Path.TrimEnd('/')
                        : request.Path + "/";
                    string altKey = request.Method + " " + altPath;

                    if (!_routes.TryGetValue(altKey, out handler))
                    {
                        AppLog.W(TAG, "No route found: " + routeKey);
                        var notFound = ServerResponse.NotFound();
                        await WriteResponseAsync(writer, notFound);
                        return;
                    }
                }

                ServerResponse resp;
                try
                {
                    resp = await handler(request);
                }
                catch (Exception ex)
                {
                    AppLog.E(TAG, "Handler error for " + routeKey, ex);
                    resp = ServerResponse.InternalError(ex.Message);
                }

                AppLog.V(TAG, "Response: " + resp.StatusCode + " for " + routeKey);
                await WriteResponseAsync(writer, resp);
            }
        }

        private async Task WriteResponseAsync(DataWriter writer, ServerResponse response)
        {
            byte[] httpBytes = response.ToHttpBytes();
            writer.WriteBytes(httpBytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
        }

        private readonly HashSet<string> _notifiedIps = new HashSet<string>();

        private void NotifyUnknownConnection(string remoteAddress)
        {
            // Debounce: only notify once per IP to avoid spamming
            if (_notifiedIps.Contains(remoteAddress)) return;
            _notifiedIps.Add(remoteAddress);

            var handler = UnknownConnectionReceived;
            if (handler != null)
                handler(remoteAddress);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
