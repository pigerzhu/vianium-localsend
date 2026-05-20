// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Vianium.LocalSend.Logging;

namespace Vianium.LocalSend.Network
{
    internal sealed class PlainHttpResponse
    {
        public int StatusCode { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public byte[] Body { get; set; }

        public PlainHttpResponse()
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Body = new byte[0];
        }

        public string GetBodyAsString()
        {
            if (Body == null || Body.Length == 0) return "";
            return Encoding.UTF8.GetString(Body, 0, Body.Length);
        }
    }

    internal sealed class PlainHttpClient
    {
        private const string TAG = "Http";
        private const int ConnectTimeoutMs = 10000;

        /// <summary>If true, connect with TLS (for devices that use "https" protocol).</summary>
        public bool UseSsl { get; set; }

        public async Task<PlainHttpResponse> GetAsync(string host, int port, string path, CancellationToken ct)
        {
            return await SendAsync(host, port, "GET", path, null, null, null, ct);
        }

        public async Task<PlainHttpResponse> PostJsonAsync(string host, int port, string path,
            string jsonBody, CancellationToken ct)
        {
            byte[] body = Encoding.UTF8.GetBytes(jsonBody);
            return await SendAsync(host, port, "POST", path, null, body, "application/json; charset=utf-8", ct);
        }

        public async Task<PlainHttpResponse> PostBinaryAsync(string host, int port, string path,
            string queryString, byte[] body, string contentType, IProgress<long> progress, CancellationToken ct)
        {
            string fullPath = queryString != null ? path + "?" + queryString : path;
            return await SendAsync(host, port, "POST", fullPath, progress, body, contentType, ct);
        }

        public async Task<PlainHttpResponse> PostFileStreamAsync(string host, int port, string path,
            string queryString, StorageFile file, long fileSize, string contentType,
            IProgress<long> progress, CancellationToken ct)
        {
            string fullPath = queryString != null ? path + "?" + queryString : path;
            AppLog.D(TAG, "-> POST (stream) " + path + " to " + host + ":" + port +
                " file=" + fileSize + "B ssl=" + UseSsl);
            var sw = Stopwatch.StartNew();

            using (var socket = new StreamSocket())
            {
                await ConnectWithTimeoutAsync(socket, host, port, ct);

                using (var writer = new DataWriter(socket.OutputStream))
                using (var reader = new DataReader(socket.InputStream))
                {
                    reader.InputStreamOptions = InputStreamOptions.Partial;

                    var headerStr = BuildRequestHeaders("POST", fullPath, host, port,
                        contentType, fileSize);
                    writer.WriteBytes(Encoding.UTF8.GetBytes(headerStr));
                    await writer.StoreAsync();

                    using (var fileStream = await file.OpenReadAsync())
                    using (var fileReader = new DataReader(fileStream))
                    {
                        long totalSent = 0;
                        long remaining = fileSize;

                        while (remaining > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            uint toRead = (uint)Math.Min(remaining, 8192);
                            uint loaded = await fileReader.LoadAsync(toRead);
                            if (loaded == 0) break;

                            byte[] chunk = new byte[loaded];
                            fileReader.ReadBytes(chunk);
                            writer.WriteBytes(chunk);
                            await writer.StoreAsync();

                            totalSent += loaded;
                            remaining -= loaded;

                            if (progress != null)
                                progress.Report(totalSent);
                        }

                        AppLog.D(TAG, "Stream body sent: " + totalSent + "B");
                    }

                    await writer.FlushAsync();

                    var response = await ReadResponseAsync(reader, ct);
                    sw.Stop();
                    long kbps = sw.ElapsedMilliseconds > 0
                        ? (fileSize * 1000L / 1024L) / sw.ElapsedMilliseconds
                        : 0;
                    AppLog.D(TAG, "<- " + response.StatusCode + " from " + host +
                        " in " + sw.ElapsedMilliseconds + "ms (" + kbps + " KB/s)");
                    return response;
                }
            }
        }

        private async Task<PlainHttpResponse> SendAsync(string host, int port, string method,
            string path, IProgress<long> progress, byte[] body, string contentType, CancellationToken ct)
        {
            long bodyLength = body != null ? body.Length : 0;
            AppLog.D(TAG, "-> " + method + " " + path + " to " + host + ":" + port +
                " (body=" + bodyLength + "B, ssl=" + UseSsl + ")");
            var sw = Stopwatch.StartNew();

            using (var socket = new StreamSocket())
            {
                await ConnectWithTimeoutAsync(socket, host, port, ct);

                using (var writer = new DataWriter(socket.OutputStream))
                using (var reader = new DataReader(socket.InputStream))
                {
                    reader.InputStreamOptions = InputStreamOptions.Partial;

                    var headerStr = BuildRequestHeaders(method, path, host, port, contentType, bodyLength);
                    writer.WriteBytes(Encoding.UTF8.GetBytes(headerStr));

                    if (body != null && body.Length > 0)
                    {
                        int chunkSize = 8192;
                        int offset = 0;
                        while (offset < body.Length)
                        {
                            ct.ThrowIfCancellationRequested();
                            int toWrite = Math.Min(chunkSize, body.Length - offset);
                            byte[] chunk = new byte[toWrite];
                            System.Buffer.BlockCopy(body, offset, chunk, 0, toWrite);
                            writer.WriteBytes(chunk);
                            await writer.StoreAsync();
                            offset += toWrite;

                            if (progress != null)
                                progress.Report(offset);
                        }
                    }

                    await writer.StoreAsync();
                    await writer.FlushAsync();

                    var response = await ReadResponseAsync(reader, ct);
                    sw.Stop();
                    AppLog.D(TAG, "<- " + response.StatusCode + " from " + host +
                        " (body=" + (response.Body != null ? response.Body.Length : 0) + "B) in " +
                        sw.ElapsedMilliseconds + "ms");
                    return response;
                }
            }
        }

        private static string BuildRequestHeaders(string method, string path, string host,
            int port, string contentType, long contentLength)
        {
            var sb = new StringBuilder();
            sb.Append(method);
            sb.Append(' ');
            sb.Append(path);
            sb.Append(" HTTP/1.1\r\n");
            sb.Append("Host: ");
            sb.Append(host);
            if (port != 80 && port != 443)
            {
                sb.Append(':');
                sb.Append(port);
            }
            sb.Append("\r\n");
            sb.Append("Connection: close\r\n");
            sb.Append("User-Agent: Vianium.LocalSend/1.0\r\n");

            if (contentLength > 0)
            {
                if (contentType != null)
                {
                    sb.Append("Content-Type: ");
                    sb.Append(contentType);
                    sb.Append("\r\n");
                }
                sb.Append("Content-Length: ");
                sb.Append(contentLength);
                sb.Append("\r\n");
            }

            sb.Append("\r\n");
            return sb.ToString();
        }

        private async Task ConnectWithTimeoutAsync(StreamSocket socket, string host, int port, CancellationToken ct)
        {
            // For HTTPS: accept self-signed certificates (LocalSend uses self-signed)
            var protection = UseSsl
                ? SocketProtectionLevel.Tls12
                : SocketProtectionLevel.PlainSocket;

            if (UseSsl)
            {
                // Ignore certificate errors for self-signed certs
                socket.Control.IgnorableServerCertificateErrors.Add(
                    Windows.Security.Cryptography.Certificates.ChainValidationResult.Untrusted);
                socket.Control.IgnorableServerCertificateErrors.Add(
                    Windows.Security.Cryptography.Certificates.ChainValidationResult.InvalidName);
                socket.Control.IgnorableServerCertificateErrors.Add(
                    Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);

                AppLog.V(TAG, "Connecting with TLS to " + host + ":" + port);
            }

            using (var timeoutCts = new CancellationTokenSource(ConnectTimeoutMs))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
            {
                try
                {
                    await socket.ConnectAsync(
                        new HostName(host),
                        port.ToString(),
                        protection).AsTask(linked.Token);

                    AppLog.V(TAG, "Connected to " + host + ":" + port +
                        (UseSsl ? " (TLS)" : " (plain)"));
                }
                catch (OperationCanceledException)
                {
                    if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                        throw new TimeoutException("Connection to " + host + ":" + port + " timed out");
                    throw;
                }
                catch (Exception ex)
                {
                    AppLog.E(TAG, "Connect failed to " + host + ":" + port +
                        (UseSsl ? " (TLS)" : " (plain)"), ex);

                    // Provide user-friendly error messages
                    string msg = ex.Message;
                    if (msg.Contains("actively refused") || msg.Contains("0x8007274D"))
                    {
                        throw new Exception(
                            "El dispositivo no acepta conexiones. " +
                            "Asegurate que LocalSend este abierto en el otro dispositivo.");
                    }
                    if (msg.Contains("unreachable") || msg.Contains("0x80072741"))
                    {
                        throw new Exception(
                            "No se puede alcanzar el dispositivo. " +
                            "Verifica que ambos estan en la misma red WiFi.");
                    }
                    throw;
                }
            }
        }

        private async Task<PlainHttpResponse> ReadResponseAsync(DataReader reader, CancellationToken ct)
        {
            var headerBuffer = new List<byte>(1024);

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                uint loaded = await reader.LoadAsync(1);
                if (loaded == 0) break;

                byte b = reader.ReadByte();
                headerBuffer.Add(b);

                int len = headerBuffer.Count;
                if (len >= 4 &&
                    headerBuffer[len - 4] == (byte)'\r' &&
                    headerBuffer[len - 3] == (byte)'\n' &&
                    headerBuffer[len - 2] == (byte)'\r' &&
                    headerBuffer[len - 1] == (byte)'\n')
                {
                    break;
                }

                if (len > 65536)
                    throw new Exception("Response headers too large");
            }

            if (headerBuffer.Count == 0)
                return new PlainHttpResponse { StatusCode = 0 };

            string headerSection = Encoding.UTF8.GetString(headerBuffer.ToArray(), 0, headerBuffer.Count);
            string[] lines = headerSection.Split(new[] { "\r\n" }, StringSplitOptions.None);

            var response = new PlainHttpResponse();

            if (lines.Length > 0)
            {
                string statusLine = lines[0];
                int firstSpace = statusLine.IndexOf(' ');
                if (firstSpace > 0)
                {
                    int secondSpace = statusLine.IndexOf(' ', firstSpace + 1);
                    string codeStr;
                    if (secondSpace > 0)
                        codeStr = statusLine.Substring(firstSpace + 1, secondSpace - firstSpace - 1);
                    else
                        codeStr = statusLine.Substring(firstSpace + 1);

                    int code;
                    if (int.TryParse(codeStr, out code))
                        response.StatusCode = code;
                }
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) break;

                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    string name = line.Substring(0, colon).Trim();
                    string value = line.Substring(colon + 1).Trim();
                    response.Headers[name] = value;
                }
            }

            // Check transfer encoding
            string transferEncoding;
            response.Headers.TryGetValue("Transfer-Encoding", out transferEncoding);
            bool isChunked = transferEncoding != null &&
                transferEncoding.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isChunked)
            {
                // Read chunked body
                response.Body = await ReadChunkedBodyAsync(reader, ct);
            }
            else
            {
                string contentLengthStr;
                if (response.Headers.TryGetValue("Content-Length", out contentLengthStr))
                {
                    int contentLength;
                    if (int.TryParse(contentLengthStr, out contentLength) && contentLength > 0)
                    {
                        byte[] body = new byte[contentLength];
                        int offset = 0;
                        while (offset < contentLength)
                        {
                            ct.ThrowIfCancellationRequested();
                            uint toRead = (uint)Math.Min(contentLength - offset, 65536);
                            uint loaded = await reader.LoadAsync(toRead);
                            if (loaded == 0) break;

                            for (uint j = 0; j < loaded && offset < contentLength; j++)
                                body[offset++] = reader.ReadByte();
                        }
                        response.Body = body;
                    }
                }
                else
                {
                    // Read until connection close
                    var bodyParts = new List<byte>();
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        uint loaded = await reader.LoadAsync(4096);
                        if (loaded == 0) break;

                        for (uint j = 0; j < loaded; j++)
                            bodyParts.Add(reader.ReadByte());
                    }
                    if (bodyParts.Count > 0)
                        response.Body = bodyParts.ToArray();
                }
            }

            return response;
        }

        /// <summary>
        /// Read HTTP chunked transfer encoding:
        /// Each chunk: hex-size\r\n, data bytes, \r\n
        /// Final chunk: 0\r\n\r\n
        /// </summary>
        private async Task<byte[]> ReadChunkedBodyAsync(DataReader reader, CancellationToken ct)
        {
            var result = new List<byte>();

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Read chunk size line (hex digits followed by \r\n)
                var sizeLine = new List<byte>();
                while (true)
                {
                    uint loaded = await reader.LoadAsync(1);
                    if (loaded == 0) return result.ToArray();

                    byte b = reader.ReadByte();
                    sizeLine.Add(b);

                    int len = sizeLine.Count;
                    if (len >= 2 && sizeLine[len - 2] == (byte)'\r' && sizeLine[len - 1] == (byte)'\n')
                        break;

                    if (len > 20) // chunk size line shouldn't be longer than this
                        return result.ToArray();
                }

                // Parse hex chunk size (strip \r\n and any chunk extensions after ;)
                string sizeStr = Encoding.UTF8.GetString(sizeLine.ToArray(), 0, sizeLine.Count - 2);
                int semiColon = sizeStr.IndexOf(';');
                if (semiColon >= 0)
                    sizeStr = sizeStr.Substring(0, semiColon);
                sizeStr = sizeStr.Trim();

                int chunkSize;
                try
                {
                    chunkSize = Convert.ToInt32(sizeStr, 16);
                }
                catch
                {
                    // Invalid chunk size — return what we have
                    return result.ToArray();
                }

                if (chunkSize == 0)
                    break; // Last chunk

                // Read chunk data
                int read = 0;
                while (read < chunkSize)
                {
                    ct.ThrowIfCancellationRequested();
                    uint toRead = (uint)Math.Min(chunkSize - read, 8192);
                    uint loaded = await reader.LoadAsync(toRead);
                    if (loaded == 0) return result.ToArray();

                    for (uint j = 0; j < loaded && read < chunkSize; j++)
                    {
                        result.Add(reader.ReadByte());
                        read++;
                    }
                }

                // Read trailing \r\n after chunk data
                uint trailLoaded = await reader.LoadAsync(2);
                if (trailLoaded >= 2)
                {
                    reader.ReadByte(); // \r
                    reader.ReadByte(); // \n
                }
            }

            // Read optional trailers + final \r\n
            try
            {
                uint finalLoaded = await reader.LoadAsync(2);
                if (finalLoaded >= 2)
                {
                    reader.ReadByte();
                    reader.ReadByte();
                }
            }
            catch { }

            return result.ToArray();
        }
    }
}
