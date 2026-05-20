// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System.Collections.Generic;
using System.Text;
using Vianium.LocalSend.Json;

namespace Vianium.LocalSend.Server
{
    internal sealed class ServerResponse
    {
        public int StatusCode { get; set; }
        public string StatusText { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public byte[] Body { get; set; }

        public ServerResponse(int statusCode, string statusText)
        {
            StatusCode = statusCode;
            StatusText = statusText;
            Headers = new Dictionary<string, string>();
            Body = new byte[0];
        }

        public static ServerResponse Json(int statusCode, JsonValue json)
        {
            string text = GetStatusText(statusCode);
            var resp = new ServerResponse(statusCode, text);
            byte[] body = Encoding.UTF8.GetBytes(SimpleJsonParser.Serialize(json));
            resp.Body = body;
            resp.Headers["Content-Type"] = "application/json; charset=utf-8";
            return resp;
        }

        public static ServerResponse Ok(JsonValue json)
        {
            return Json(200, json);
        }

        public static ServerResponse NoContent()
        {
            return new ServerResponse(204, "No Content");
        }

        public static ServerResponse BadRequest(string message)
        {
            var obj = new JsonObject();
            obj.Set("message", new JsonString(message));
            return Json(400, obj);
        }

        public static ServerResponse Unauthorized(string message)
        {
            var obj = new JsonObject();
            obj.Set("message", new JsonString(message));
            return Json(401, obj);
        }

        public static ServerResponse Forbidden(string message)
        {
            var obj = new JsonObject();
            obj.Set("message", new JsonString(message));
            return Json(403, obj);
        }

        public static ServerResponse NotFound()
        {
            return new ServerResponse(404, "Not Found");
        }

        public static ServerResponse Conflict(string message)
        {
            var obj = new JsonObject();
            obj.Set("message", new JsonString(message));
            return Json(409, obj);
        }

        public static ServerResponse PreconditionFailed()
        {
            return new ServerResponse(412, "Precondition Failed");
        }

        public static ServerResponse InternalError(string message)
        {
            var obj = new JsonObject();
            obj.Set("message", new JsonString(message));
            return Json(500, obj);
        }

        public byte[] ToHttpBytes()
        {
            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 ");
            sb.Append(StatusCode);
            sb.Append(' ');
            sb.Append(StatusText);
            sb.Append("\r\n");

            if (Body != null && Body.Length > 0)
            {
                Headers["Content-Length"] = Body.Length.ToString();
            }
            else
            {
                Headers["Content-Length"] = "0";
            }

            Headers["Connection"] = "close";

            foreach (var header in Headers)
            {
                sb.Append(header.Key);
                sb.Append(": ");
                sb.Append(header.Value);
                sb.Append("\r\n");
            }

            sb.Append("\r\n");

            byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());

            if (Body != null && Body.Length > 0)
            {
                byte[] result = new byte[headerBytes.Length + Body.Length];
                System.Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
                System.Buffer.BlockCopy(Body, 0, result, headerBytes.Length, Body.Length);
                return result;
            }

            return headerBytes;
        }

        private static string GetStatusText(int code)
        {
            switch (code)
            {
                case 200: return "OK";
                case 204: return "No Content";
                case 400: return "Bad Request";
                case 401: return "Unauthorized";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 408: return "Request Timeout";
                case 409: return "Conflict";
                case 412: return "Precondition Failed";
                case 429: return "Too Many Requests";
                case 500: return "Internal Server Error";
                default: return "Unknown";
            }
        }
    }
}
