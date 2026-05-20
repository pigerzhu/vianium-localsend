// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Text;

namespace Vianium.LocalSend.Server
{
    internal sealed class ServerRequest
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public string QueryString { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public byte[] Body { get; set; }
        public string RemoteAddress { get; set; }

        public ServerRequest()
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Body = new byte[0];
        }

        public string GetBodyAsString()
        {
            if (Body == null || Body.Length == 0) return "";
            return Encoding.UTF8.GetString(Body, 0, Body.Length);
        }

        public string GetQueryParam(string key)
        {
            if (string.IsNullOrEmpty(QueryString)) return null;

            string[] pairs = QueryString.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                int eq = pairs[i].IndexOf('=');
                if (eq < 0) continue;

                string k = Uri.UnescapeDataString(pairs[i].Substring(0, eq));
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pairs[i].Substring(eq + 1));
                }
            }
            return null;
        }

        public int ContentLength
        {
            get
            {
                string val;
                if (Headers.TryGetValue("Content-Length", out val))
                {
                    int len;
                    if (int.TryParse(val, out len))
                        return len;
                }
                return 0;
            }
        }
    }
}
