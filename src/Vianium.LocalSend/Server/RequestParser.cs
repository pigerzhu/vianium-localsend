// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>
// SPDX-FileCopyrightText: 2026 pigerzhu

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace Vianium.LocalSend.Server
{
    internal static class RequestParser
    {
        /// <summary>
        /// Parse only the request line and headers. Does NOT read the body.
        /// </summary>
        public static async Task<ServerRequest> ParseHeadersAsync(DataReader reader)
        {
            var headerBuffer = new List<byte>(1024);

            while (true)
            {
                uint loaded = await reader.LoadAsync(1);
                if (loaded == 0)
                {
                    // HTTPS clients connecting to our HTTP server will close immediately
                    // after TLS handshake fails — this is expected, not an error
                    if (headerBuffer.Count == 0)
                        return null;
                    throw new Exception("Connection closed before headers complete");
                }

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
                    throw new Exception("Request headers too large");
            }

            string headerSection = Encoding.UTF8.GetString(headerBuffer.ToArray(), 0, headerBuffer.Count);
            string[] lines = headerSection.Split(new[] { "\r\n" }, StringSplitOptions.None);

            if (lines.Length < 1)
                throw new Exception("Empty request");

            var request = new ServerRequest();
            ParseRequestLine(request, lines[0]);

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) break;

                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    string name = line.Substring(0, colon).Trim();
                    string value = line.Substring(colon + 1).Trim();
                    request.Headers[name] = value;
                }
            }

            return request;
        }

        /// <summary>
        /// Read the full body into memory. For small requests (JSON metadata).
        /// </summary>
        public static async Task<byte[]> ReadBodyAsync(DataReader reader, int contentLength)
        {
            if (contentLength <= 0) return new byte[0];
            if (contentLength > 100 * 1024 * 1024)
                throw new Exception("Request body too large");

            byte[] body = new byte[contentLength];
            int offset = 0;
            while (offset < contentLength)
            {
                uint toRead = (uint)Math.Min(contentLength - offset, 65536);
                uint loaded = await reader.LoadAsync(toRead);
                if (loaded == 0)
                    throw new Exception("Connection closed before body complete");

                for (uint j = 0; j < loaded; j++)
                {
                    body[offset++] = reader.ReadByte();
                }
            }
            return body;
        }

        /// <summary>
        /// Legacy: parse headers + body in one call. Kept for backward compat.
        /// </summary>
        public static async Task<ServerRequest> ParseAsync(DataReader reader)
        {
            var request = await ParseHeadersAsync(reader);

            int contentLength = request.ContentLength;
            if (contentLength > 0)
            {
                request.Body = await ReadBodyAsync(reader, contentLength);
            }

            return request;
        }

        public static async Task<string> ReadLineAsync(DataReader reader)
        {
            var bytes = new List<byte>();
            while (true)
            {
                if (await reader.LoadAsync(1) == 0) throw new Exception("Truncated chunk header");
                byte b = reader.ReadByte();
                if (b == 10)
                {
                    if (bytes.Count == 0 || bytes[bytes.Count - 1] != 13)
                        throw new Exception("Invalid chunk line ending");
                    bytes.RemoveAt(bytes.Count - 1);
                    return Encoding.UTF8.GetString(bytes.ToArray(), 0, bytes.Count);
                }
                bytes.Add(b);
                if (bytes.Count > 8192) throw new Exception("Chunk header too large");
            }
        }

        public static long ParseChunkSize(string line)
        {
            long size;
            if (!long.TryParse(line.Split(';')[0].Trim(), System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out size) || size < 0)
                throw new Exception("Invalid chunk size");
            return size;
        }

        private static void ParseRequestLine(ServerRequest request, string line)
        {
            int firstSpace = line.IndexOf(' ');
            if (firstSpace < 0)
                throw new Exception("Invalid request line: " + line);

            request.Method = line.Substring(0, firstSpace).ToUpperInvariant();

            int secondSpace = line.IndexOf(' ', firstSpace + 1);
            string uri;
            if (secondSpace > 0)
                uri = line.Substring(firstSpace + 1, secondSpace - firstSpace - 1);
            else
                uri = line.Substring(firstSpace + 1);

            int questionMark = uri.IndexOf('?');
            if (questionMark >= 0)
            {
                request.Path = uri.Substring(0, questionMark);
                request.QueryString = uri.Substring(questionMark + 1);
            }
            else
            {
                request.Path = uri;
                request.QueryString = null;
            }
        }
    }
}
