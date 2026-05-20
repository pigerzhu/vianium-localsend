// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Vianium.LocalSend.Json
{
    #region JsonValue Hierarchy

    /// <summary>
    /// Base class for all JSON value types.
    /// </summary>
    public abstract class JsonValue
    {
        /// <summary>Gets whether this value is a JSON null.</summary>
        public virtual bool IsNull { get { return false; } }

        /// <summary>Gets whether this value is a JSON boolean.</summary>
        public virtual bool IsBoolean { get { return false; } }

        /// <summary>Gets whether this value is a JSON number.</summary>
        public virtual bool IsNumber { get { return false; } }

        /// <summary>Gets whether this value is a JSON string.</summary>
        public virtual bool IsString { get { return false; } }

        /// <summary>Gets whether this value is a JSON object.</summary>
        public virtual bool IsObject { get { return false; } }

        /// <summary>Gets whether this value is a JSON array.</summary>
        public virtual bool IsArray { get { return false; } }

        /// <summary>
        /// Accesses a field by key. Only valid on JsonObject.
        /// </summary>
        public virtual JsonValue this[string key]
        {
            get { throw new InvalidOperationException("Cannot access field on " + GetType().Name); }
        }

        /// <summary>
        /// Accesses an element by index. Only valid on JsonArray.
        /// </summary>
        public virtual JsonValue this[int index]
        {
            get { throw new InvalidOperationException("Cannot access index on " + GetType().Name); }
        }

        /// <summary>Returns the value as a string. Only valid on JsonString.</summary>
        public virtual string AsString()
        {
            throw new InvalidOperationException("Cannot convert " + GetType().Name + " to string");
        }

        /// <summary>Returns the value as a double. Only valid on JsonNumber.</summary>
        public virtual double AsDouble()
        {
            throw new InvalidOperationException("Cannot convert " + GetType().Name + " to double");
        }

        /// <summary>Returns the value as a long. Only valid on JsonNumber.</summary>
        public virtual long AsLong()
        {
            throw new InvalidOperationException("Cannot convert " + GetType().Name + " to long");
        }

        /// <summary>Returns the value as an int. Only valid on JsonNumber.</summary>
        public virtual int AsInt()
        {
            throw new InvalidOperationException("Cannot convert " + GetType().Name + " to int");
        }

        /// <summary>Returns the value as a bool. Only valid on JsonBoolean.</summary>
        public virtual bool AsBool()
        {
            throw new InvalidOperationException("Cannot convert " + GetType().Name + " to bool");
        }

        /// <summary>Returns the value as a list. Only valid on JsonArray.</summary>
        public virtual List<JsonValue> AsArray()
        {
            throw new InvalidOperationException("Cannot convert " + GetType().Name + " to array");
        }

        /// <summary>Returns the value as a dictionary. Only valid on JsonObject.</summary>
        public virtual Dictionary<string, JsonValue> AsObject()
        {
            throw new InvalidOperationException("Cannot convert " + GetType().Name + " to object");
        }

        /// <summary>Returns the number of elements (JsonArray) or fields (JsonObject).</summary>
        public virtual int Count
        {
            get { throw new InvalidOperationException("Cannot get count on " + GetType().Name); }
        }

        /// <summary>Checks if a JsonObject contains the given key.</summary>
        public virtual bool ContainsKey(string key)
        {
            throw new InvalidOperationException("Cannot check key on " + GetType().Name);
        }

        /// <summary>Gets the ordered list of keys. Only valid on JsonObject.</summary>
        public virtual IList<string> Keys
        {
            get { throw new InvalidOperationException("Cannot get keys on " + GetType().Name); }
        }
    }

    /// <summary>
    /// Represents a JSON null value.
    /// </summary>
    public sealed class JsonNull : JsonValue
    {
        /// <summary>Singleton instance of JsonNull.</summary>
        public static readonly JsonNull Instance = new JsonNull();

        private JsonNull() { }

        public override bool IsNull { get { return true; } }

        public override string ToString()
        {
            return "null";
        }
    }

    /// <summary>
    /// Represents a JSON boolean value.
    /// </summary>
    public sealed class JsonBoolean : JsonValue
    {
        /// <summary>Singleton for true.</summary>
        public static readonly JsonBoolean True = new JsonBoolean(true);

        /// <summary>Singleton for false.</summary>
        public static readonly JsonBoolean False = new JsonBoolean(false);

        private readonly bool _value;

        private JsonBoolean(bool value)
        {
            _value = value;
        }

        public override bool IsBoolean { get { return true; } }

        public override bool AsBool()
        {
            return _value;
        }

        /// <summary>Gets the boolean value.</summary>
        public bool Value { get { return _value; } }

        public override string ToString()
        {
            return _value ? "true" : "false";
        }
    }

    /// <summary>
    /// Represents a JSON number value. Stored as double.
    /// </summary>
    public sealed class JsonNumber : JsonValue
    {
        private readonly double _value;

        /// <summary>
        /// Creates a new JsonNumber.
        /// </summary>
        public JsonNumber(double value)
        {
            _value = value;
        }

        public override bool IsNumber { get { return true; } }

        /// <summary>Gets whether the number has no fractional part.</summary>
        public bool IsInteger
        {
            get { return _value == Math.Floor(_value) && !double.IsInfinity(_value); }
        }

        /// <summary>Gets the underlying double value.</summary>
        public double Value { get { return _value; } }

        public override double AsDouble()
        {
            return _value;
        }

        public override long AsLong()
        {
            return (long)_value;
        }

        public override int AsInt()
        {
            return (int)_value;
        }

        public override string ToString()
        {
            if (IsInteger)
            {
                return ((long)_value).ToString(CultureInfo.InvariantCulture);
            }
            return _value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Represents a JSON string value.
    /// </summary>
    public sealed class JsonString : JsonValue
    {
        private readonly string _value;

        /// <summary>
        /// Creates a new JsonString.
        /// </summary>
        public JsonString(string value)
        {
            if (value == null) throw new ArgumentNullException("value");
            _value = value;
        }

        public override bool IsString { get { return true; } }

        /// <summary>Gets the underlying string value.</summary>
        public string Value { get { return _value; } }

        public override string AsString()
        {
            return _value;
        }

        public override string ToString()
        {
            return "\"" + SimpleJsonParser.EscapeString(_value) + "\"";
        }
    }

    /// <summary>
    /// Represents a JSON object with ordered keys.
    /// </summary>
    public sealed class JsonObject : JsonValue
    {
        private readonly Dictionary<string, JsonValue> _map;
        private readonly List<string> _keys;

        /// <summary>
        /// Creates a new empty JsonObject.
        /// </summary>
        public JsonObject()
        {
            _map = new Dictionary<string, JsonValue>();
            _keys = new List<string>();
        }

        public override bool IsObject { get { return true; } }

        public override JsonValue this[string key]
        {
            get
            {
                JsonValue val;
                if (_map.TryGetValue(key, out val))
                {
                    return val;
                }
                throw new KeyNotFoundException(string.Format("Key '{0}' not found in JSON object", key));
            }
        }

        /// <summary>
        /// Sets a field value. If the key already exists, updates it in place.
        /// </summary>
        public void Set(string key, JsonValue value)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (value == null) throw new ArgumentNullException("value");

            if (_map.ContainsKey(key))
            {
                _map[key] = value;
            }
            else
            {
                _map[key] = value;
                _keys.Add(key);
            }
        }

        public override int Count { get { return _keys.Count; } }

        public override bool ContainsKey(string key)
        {
            return _map.ContainsKey(key);
        }

        public override IList<string> Keys { get { return _keys; } }

        public override Dictionary<string, JsonValue> AsObject()
        {
            return _map;
        }

        public override string ToString()
        {
            return SimpleJsonParser.Serialize(this);
        }
    }

    /// <summary>
    /// Represents a JSON array.
    /// </summary>
    public sealed class JsonArray : JsonValue
    {
        private readonly List<JsonValue> _items;

        /// <summary>
        /// Creates a new empty JsonArray.
        /// </summary>
        public JsonArray()
        {
            _items = new List<JsonValue>();
        }

        public override bool IsArray { get { return true; } }

        public override JsonValue this[int index]
        {
            get { return _items[index]; }
        }

        /// <summary>
        /// Adds a value to the array.
        /// </summary>
        public void Add(JsonValue value)
        {
            if (value == null) throw new ArgumentNullException("value");
            _items.Add(value);
        }

        public override int Count { get { return _items.Count; } }

        public override List<JsonValue> AsArray()
        {
            return _items;
        }

        public override string ToString()
        {
            return SimpleJsonParser.Serialize(this);
        }
    }

    #endregion

    #region JsonParseException

    /// <summary>
    /// Exception thrown when JSON parsing fails, with position information.
    /// </summary>
    public class JsonParseException : Exception
    {
        /// <summary>Absolute character position in the input string.</summary>
        public int Position { get; private set; }

        /// <summary>Line number (1-based) where the error occurred.</summary>
        public int Line { get; private set; }

        /// <summary>Column number (1-based) where the error occurred.</summary>
        public int Column { get; private set; }

        /// <summary>
        /// Creates a new JsonParseException.
        /// </summary>
        public JsonParseException(string message, int position, int line, int column)
            : base(string.Format("{0} at line {1}, column {2} (position {3})", message, line, column, position))
        {
            Position = position;
            Line = line;
            Column = column;
        }
    }

    #endregion

    #region SimpleJsonParser

    /// <summary>
    /// A complete JSON parser and serializer with zero external dependencies.
    /// </summary>
    public static class SimpleJsonParser
    {
        #region Parsing

        /// <summary>
        /// Parses a JSON string into a JsonValue.
        /// </summary>
        public static JsonValue Parse(string json)
        {
            if (json == null) throw new ArgumentNullException("json");

            var parser = new JsonParser(json);
            JsonValue result = parser.ParseValue();
            parser.SkipWhitespace();

            if (parser.HasMore())
            {
                parser.ThrowError("Unexpected content after JSON value");
            }

            return result;
        }

        /// <summary>
        /// Tries to parse a JSON string without throwing an exception.
        /// </summary>
        public static bool TryParse(string json, out JsonValue value)
        {
            if (json == null)
            {
                value = null;
                return false;
            }

            try
            {
                value = Parse(json);
                return true;
            }
            catch (JsonParseException)
            {
                value = null;
                return false;
            }
        }

        private class JsonParser
        {
            private readonly string _json;
            private int _pos;
            private int _line;
            private int _col;

            public JsonParser(string json)
            {
                _json = json;
                _pos = 0;
                _line = 1;
                _col = 1;
            }

            public bool HasMore()
            {
                return _pos < _json.Length;
            }

            public void ThrowError(string message)
            {
                throw new JsonParseException(message, _pos, _line, _col);
            }

            private char PeekChar()
            {
                if (_pos >= _json.Length)
                {
                    ThrowError("Unexpected end of input");
                }
                return _json[_pos];
            }

            private char ReadChar()
            {
                if (_pos >= _json.Length)
                {
                    ThrowError("Unexpected end of input");
                }
                char c = _json[_pos];
                _pos++;
                if (c == '\n')
                {
                    _line++;
                    _col = 1;
                }
                else
                {
                    _col++;
                }
                return c;
            }

            public void SkipWhitespace()
            {
                while (_pos < _json.Length)
                {
                    char c = _json[_pos];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    {
                        ReadChar();
                    }
                    else
                    {
                        break;
                    }
                }
            }

            public JsonValue ParseValue()
            {
                SkipWhitespace();

                if (_pos >= _json.Length)
                {
                    ThrowError("Unexpected end of input, expected a JSON value");
                }

                char c = PeekChar();

                switch (c)
                {
                    case '"':
                        return ParseString();
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case 't':
                        return ParseLiteral("true", JsonBoolean.True);
                    case 'f':
                        return ParseLiteral("false", JsonBoolean.False);
                    case 'n':
                        return ParseLiteral("null", JsonNull.Instance);
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            return ParseNumber();
                        }
                        ThrowError(string.Format("Unexpected character '{0}'", c));
                        return null;
                }
            }

            private JsonValue ParseString()
            {
                ReadChar();
                var sb = new StringBuilder();

                while (true)
                {
                    if (_pos >= _json.Length)
                    {
                        ThrowError("Unterminated string");
                    }

                    char c = ReadChar();

                    if (c == '"')
                    {
                        return new JsonString(sb.ToString());
                    }

                    if (c == '\\')
                    {
                        if (_pos >= _json.Length)
                        {
                            ThrowError("Unterminated escape sequence");
                        }

                        char esc = ReadChar();
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                int codePoint = ReadHex4();
                                if (codePoint >= 0xD800 && codePoint <= 0xDBFF)
                                {
                                    if (_pos + 1 < _json.Length && _json[_pos] == '\\' && _json[_pos + 1] == 'u')
                                    {
                                        ReadChar();
                                        ReadChar();
                                        int low = ReadHex4();
                                        if (low >= 0xDC00 && low <= 0xDFFF)
                                        {
                                            int full = 0x10000 + ((codePoint - 0xD800) << 10) + (low - 0xDC00);
                                            sb.Append(char.ConvertFromUtf32(full));
                                        }
                                        else
                                        {
                                            ThrowError("Invalid low surrogate");
                                        }
                                    }
                                    else
                                    {
                                        ThrowError("Expected low surrogate after high surrogate");
                                    }
                                }
                                else if (codePoint >= 0xDC00 && codePoint <= 0xDFFF)
                                {
                                    ThrowError("Unexpected low surrogate without high surrogate");
                                }
                                else
                                {
                                    sb.Append((char)codePoint);
                                }
                                break;
                            default:
                                ThrowError(string.Format("Invalid escape character '\\{0}'", esc));
                                break;
                        }
                    }
                    else if (c < 0x20)
                    {
                        ThrowError(string.Format("Unescaped control character (0x{0:X2}) in string", (int)c));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            private int ReadHex4()
            {
                if (_pos + 4 > _json.Length)
                {
                    ThrowError("Incomplete unicode escape sequence");
                }

                int value = 0;
                for (int i = 0; i < 4; i++)
                {
                    char c = ReadChar();
                    int digit;
                    if (c >= '0' && c <= '9')
                    {
                        digit = c - '0';
                    }
                    else if (c >= 'a' && c <= 'f')
                    {
                        digit = 10 + (c - 'a');
                    }
                    else if (c >= 'A' && c <= 'F')
                    {
                        digit = 10 + (c - 'A');
                    }
                    else
                    {
                        ThrowError(string.Format("Invalid hex character '{0}' in unicode escape", c));
                        digit = 0;
                    }
                    value = (value << 4) | digit;
                }
                return value;
            }

            private JsonValue ParseNumber()
            {
                int start = _pos;
                int startLine = _line;
                int startCol = _col;

                if (PeekChar() == '-')
                {
                    ReadChar();
                }

                if (_pos < _json.Length && PeekChar() == '0')
                {
                    ReadChar();
                }
                else if (_pos < _json.Length && PeekChar() >= '1' && PeekChar() <= '9')
                {
                    ReadChar();
                    while (_pos < _json.Length && _json[_pos] >= '0' && _json[_pos] <= '9')
                    {
                        ReadChar();
                    }
                }
                else
                {
                    ThrowError("Invalid number");
                }

                if (_pos < _json.Length && _json[_pos] == '.')
                {
                    ReadChar();
                    if (_pos >= _json.Length || _json[_pos] < '0' || _json[_pos] > '9')
                    {
                        ThrowError("Expected digit after decimal point");
                    }
                    while (_pos < _json.Length && _json[_pos] >= '0' && _json[_pos] <= '9')
                    {
                        ReadChar();
                    }
                }

                if (_pos < _json.Length && (_json[_pos] == 'e' || _json[_pos] == 'E'))
                {
                    ReadChar();
                    if (_pos < _json.Length && (_json[_pos] == '+' || _json[_pos] == '-'))
                    {
                        ReadChar();
                    }
                    if (_pos >= _json.Length || _json[_pos] < '0' || _json[_pos] > '9')
                    {
                        ThrowError("Expected digit in exponent");
                    }
                    while (_pos < _json.Length && _json[_pos] >= '0' && _json[_pos] <= '9')
                    {
                        ReadChar();
                    }
                }

                string raw = _json.Substring(start, _pos - start);
                double value;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    throw new JsonParseException(
                        string.Format("Invalid number '{0}'", raw),
                        start, startLine, startCol);
                }

                return new JsonNumber(value);
            }

            private JsonValue ParseObject()
            {
                ReadChar();
                SkipWhitespace();

                var obj = new JsonObject();

                if (_pos < _json.Length && PeekChar() == '}')
                {
                    ReadChar();
                    return obj;
                }

                while (true)
                {
                    SkipWhitespace();

                    if (PeekChar() != '"')
                    {
                        ThrowError("Expected string key in object");
                    }
                    string key = ((JsonString)ParseString()).Value;

                    SkipWhitespace();
                    if (ReadChar() != ':')
                    {
                        ThrowError("Expected ':' after object key");
                    }

                    JsonValue val = ParseValue();
                    obj.Set(key, val);

                    SkipWhitespace();
                    char next = ReadChar();
                    if (next == '}')
                    {
                        return obj;
                    }
                    if (next != ',')
                    {
                        ThrowError("Expected ',' or '}' in object");
                    }
                }
            }

            private JsonValue ParseArray()
            {
                ReadChar();
                SkipWhitespace();

                var arr = new JsonArray();

                if (_pos < _json.Length && PeekChar() == ']')
                {
                    ReadChar();
                    return arr;
                }

                while (true)
                {
                    JsonValue val = ParseValue();
                    arr.Add(val);

                    SkipWhitespace();
                    char next = ReadChar();
                    if (next == ']')
                    {
                        return arr;
                    }
                    if (next != ',')
                    {
                        ThrowError("Expected ',' or ']' in array");
                    }
                }
            }

            private JsonValue ParseLiteral(string expected, JsonValue result)
            {
                for (int i = 0; i < expected.Length; i++)
                {
                    if (_pos >= _json.Length || ReadChar() != expected[i])
                    {
                        ThrowError(string.Format("Expected '{0}'", expected));
                    }
                }
                return result;
            }
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Serializes a JsonValue to compact JSON.
        /// </summary>
        public static string Serialize(JsonValue value)
        {
            if (value == null) throw new ArgumentNullException("value");
            var sb = new StringBuilder();
            SerializeValue(sb, value);
            return sb.ToString();
        }

        /// <summary>
        /// Serializes a JsonValue to pretty-printed JSON with indentation.
        /// </summary>
        public static string SerializePretty(JsonValue value, int indent)
        {
            if (value == null) throw new ArgumentNullException("value");
            var sb = new StringBuilder();
            SerializePrettyValue(sb, value, 0, indent);
            return sb.ToString();
        }

        private static void SerializeValue(StringBuilder sb, JsonValue value)
        {
            if (value.IsNull)
            {
                sb.Append("null");
            }
            else if (value.IsBoolean)
            {
                sb.Append(value.AsBool() ? "true" : "false");
            }
            else if (value.IsNumber)
            {
                var num = (JsonNumber)value;
                if (num.IsInteger)
                {
                    sb.Append(num.AsLong().ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(num.Value.ToString("R", CultureInfo.InvariantCulture));
                }
            }
            else if (value.IsString)
            {
                sb.Append('"');
                sb.Append(EscapeString(value.AsString()));
                sb.Append('"');
            }
            else if (value.IsObject)
            {
                sb.Append('{');
                IList<string> keys = value.Keys;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('"');
                    sb.Append(EscapeString(keys[i]));
                    sb.Append('"');
                    sb.Append(':');
                    SerializeValue(sb, value[keys[i]]);
                }
                sb.Append('}');
            }
            else if (value.IsArray)
            {
                sb.Append('[');
                int count = value.Count;
                for (int i = 0; i < count; i++)
                {
                    if (i > 0) sb.Append(',');
                    SerializeValue(sb, value[i]);
                }
                sb.Append(']');
            }
        }

        private static void SerializePrettyValue(StringBuilder sb, JsonValue value, int depth, int indent)
        {
            if (value.IsNull)
            {
                sb.Append("null");
            }
            else if (value.IsBoolean)
            {
                sb.Append(value.AsBool() ? "true" : "false");
            }
            else if (value.IsNumber)
            {
                var num = (JsonNumber)value;
                if (num.IsInteger)
                {
                    sb.Append(num.AsLong().ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(num.Value.ToString("R", CultureInfo.InvariantCulture));
                }
            }
            else if (value.IsString)
            {
                sb.Append('"');
                sb.Append(EscapeString(value.AsString()));
                sb.Append('"');
            }
            else if (value.IsObject)
            {
                IList<string> keys = value.Keys;
                if (keys.Count == 0)
                {
                    sb.Append("{}");
                    return;
                }

                sb.Append("{\n");
                string childIndent = new string(' ', (depth + 1) * indent);
                string closingIndent = new string(' ', depth * indent);

                for (int i = 0; i < keys.Count; i++)
                {
                    sb.Append(childIndent);
                    sb.Append('"');
                    sb.Append(EscapeString(keys[i]));
                    sb.Append("\": ");
                    SerializePrettyValue(sb, value[keys[i]], depth + 1, indent);
                    if (i < keys.Count - 1) sb.Append(',');
                    sb.Append('\n');
                }

                sb.Append(closingIndent);
                sb.Append('}');
            }
            else if (value.IsArray)
            {
                int count = value.Count;
                if (count == 0)
                {
                    sb.Append("[]");
                    return;
                }

                sb.Append("[\n");
                string childIndent = new string(' ', (depth + 1) * indent);
                string closingIndent = new string(' ', depth * indent);

                for (int i = 0; i < count; i++)
                {
                    sb.Append(childIndent);
                    SerializePrettyValue(sb, value[i], depth + 1, indent);
                    if (i < count - 1) sb.Append(',');
                    sb.Append('\n');
                }

                sb.Append(closingIndent);
                sb.Append(']');
            }
        }

        /// <summary>
        /// Escapes a string for JSON output.
        /// </summary>
        internal static string EscapeString(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append(string.Format("\\u{0:X4}", (int)c));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        #endregion
    }

    #endregion
}
