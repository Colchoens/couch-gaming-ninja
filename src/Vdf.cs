using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CouchGamingNinja
{
    /// <summary>An ordered key/value node, used for both Steam's binary VDF (shortcuts.vdf) and text VDF (localconfig.vdf).</summary>
    public class VdfNode
    {
        public enum Kind : byte { Map = 0x00, String = 0x01, Int32 = 0x02, Float = 0x03, Ptr = 0x04, WString = 0x05, Color = 0x06, UInt64 = 0x07, Int64 = 0x0A }

        public Kind Type;
        public string Str;
        public int Int;
        public float Flt;
        public ulong U64;
        public long I64;
        public byte[] Raw; // ptr/color
        public List<KeyValuePair<string, VdfNode>> Children = new List<KeyValuePair<string, VdfNode>>();

        public static VdfNode NewMap() { return new VdfNode { Type = Kind.Map }; }
        public static VdfNode NewString(string s) { return new VdfNode { Type = Kind.String, Str = s ?? "" }; }
        public static VdfNode NewInt(int i) { return new VdfNode { Type = Kind.Int32, Int = i }; }

        public VdfNode Get(string key)
        {
            foreach (var kv in Children)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        public void Set(string key, VdfNode value)
        {
            for (int i = 0; i < Children.Count; i++)
                if (string.Equals(Children[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    Children[i] = new KeyValuePair<string, VdfNode>(Children[i].Key, value);
                    return;
                }
            Children.Add(new KeyValuePair<string, VdfNode>(key, value));
        }

        public VdfNode GetOrAddMap(string key)
        {
            var n = Get(key);
            if (n == null || n.Type != Kind.Map) { n = NewMap(); Set(key, n); }
            return n;
        }

        public string GetString(string key)
        {
            var n = Get(key);
            return n == null ? null : (n.Type == Kind.String ? n.Str : n.Type == Kind.Int32 ? n.Int.ToString() : null);
        }
    }

    public static class BinaryVdf
    {
        static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static VdfNode Read(byte[] data)
        {
            int pos = 0;
            var root = VdfNode.NewMap();
            ReadMap(data, ref pos, root);
            return root;
        }

        static void ReadMap(byte[] d, ref int pos, VdfNode map)
        {
            while (pos < d.Length)
            {
                byte t = d[pos++];
                if (t == 0x08) return;
                string key = ReadCString(d, ref pos);
                var node = new VdfNode { Type = (VdfNode.Kind)t };
                switch (t)
                {
                    case 0x00: ReadMap(d, ref pos, node); break;
                    case 0x01: node.Str = ReadCString(d, ref pos); break;
                    case 0x02: node.Int = BitConverter.ToInt32(d, pos); pos += 4; break;
                    case 0x03: node.Flt = BitConverter.ToSingle(d, pos); pos += 4; break;
                    case 0x04: case 0x06: node.Raw = Slice(d, pos, 4); pos += 4; break;
                    case 0x05: node.Str = ReadWString(d, ref pos); break;
                    case 0x07: node.U64 = BitConverter.ToUInt64(d, pos); pos += 8; break;
                    case 0x0A: node.I64 = BitConverter.ToInt64(d, pos); pos += 8; break;
                    default: throw new InvalidDataException("Unknown VDF type 0x" + t.ToString("X2") + " at " + (pos - 1));
                }
                map.Children.Add(new KeyValuePair<string, VdfNode>(key, node));
            }
        }

        static byte[] Slice(byte[] d, int pos, int n) { var b = new byte[n]; Array.Copy(d, pos, b, 0, n); return b; }

        static string ReadCString(byte[] d, ref int pos)
        {
            int start = pos;
            while (d[pos] != 0) pos++;
            string s = Utf8.GetString(d, start, pos - start);
            pos++;
            return s;
        }

        static string ReadWString(byte[] d, ref int pos)
        {
            int start = pos;
            while (!(d[pos] == 0 && d[pos + 1] == 0)) pos += 2;
            string s = Encoding.Unicode.GetString(d, start, pos - start);
            pos += 2;
            return s;
        }

        public static byte[] Write(VdfNode root)
        {
            using (var ms = new MemoryStream())
            {
                WriteMapBody(ms, root);
                return ms.ToArray();
            }
        }

        static void WriteMapBody(Stream s, VdfNode map)
        {
            foreach (var kv in map.Children)
            {
                var n = kv.Value;
                s.WriteByte((byte)n.Type);
                WriteCString(s, kv.Key);
                switch (n.Type)
                {
                    case VdfNode.Kind.Map: WriteMapBody(s, n); break;
                    case VdfNode.Kind.String: WriteCString(s, n.Str); break;
                    case VdfNode.Kind.Int32: s.Write(BitConverter.GetBytes(n.Int), 0, 4); break;
                    case VdfNode.Kind.Float: s.Write(BitConverter.GetBytes(n.Flt), 0, 4); break;
                    case VdfNode.Kind.Ptr: case VdfNode.Kind.Color: s.Write(n.Raw, 0, 4); break;
                    case VdfNode.Kind.WString: var w = Encoding.Unicode.GetBytes(n.Str); s.Write(w, 0, w.Length); s.WriteByte(0); s.WriteByte(0); break;
                    case VdfNode.Kind.UInt64: s.Write(BitConverter.GetBytes(n.U64), 0, 8); break;
                    case VdfNode.Kind.Int64: s.Write(BitConverter.GetBytes(n.I64), 0, 8); break;
                }
            }
            s.WriteByte(0x08);
        }

        static void WriteCString(Stream s, string v)
        {
            var b = Utf8.GetBytes(v ?? "");
            s.Write(b, 0, b.Length);
            s.WriteByte(0);
        }
    }

    /// <summary>Steam's text KeyValues format ("key" "value" / "key" { ... }).</summary>
    public static class TextVdf
    {
        public static VdfNode Parse(string text)
        {
            int pos = 0;
            var root = VdfNode.NewMap();
            ParseBody(text, ref pos, root, true);
            return root;
        }

        static void ParseBody(string t, ref int pos, VdfNode map, bool top)
        {
            while (true)
            {
                SkipWs(t, ref pos);
                if (pos >= t.Length) { if (top) return; throw new InvalidDataException("Unexpected end of VDF"); }
                if (t[pos] == '}') { pos++; return; }
                string key = ReadToken(t, ref pos);
                SkipWs(t, ref pos);
                if (pos < t.Length && t[pos] == '{')
                {
                    pos++;
                    var child = VdfNode.NewMap();
                    ParseBody(t, ref pos, child, false);
                    map.Children.Add(new KeyValuePair<string, VdfNode>(key, child));
                }
                else
                {
                    string val = ReadToken(t, ref pos);
                    map.Children.Add(new KeyValuePair<string, VdfNode>(key, VdfNode.NewString(val)));
                    // optional conditional like [$WIN32]
                    SkipInlineWs(t, ref pos);
                    if (pos < t.Length && t[pos] == '[') { while (pos < t.Length && t[pos] != ']') pos++; pos++; }
                }
            }
        }

        static void SkipInlineWs(string t, ref int pos)
        {
            while (pos < t.Length && (t[pos] == ' ' || t[pos] == '\t')) pos++;
        }

        static void SkipWs(string t, ref int pos)
        {
            while (pos < t.Length)
            {
                if (char.IsWhiteSpace(t[pos])) { pos++; continue; }
                if (t[pos] == '/' && pos + 1 < t.Length && t[pos + 1] == '/') { while (pos < t.Length && t[pos] != '\n') pos++; continue; }
                break;
            }
        }

        static string ReadToken(string t, ref int pos)
        {
            SkipWs(t, ref pos);
            var sb = new StringBuilder();
            if (t[pos] == '"')
            {
                pos++;
                while (pos < t.Length && t[pos] != '"')
                {
                    if (t[pos] == '\\' && pos + 1 < t.Length)
                    {
                        char c = t[pos + 1];
                        sb.Append(c == 'n' ? '\n' : c == 't' ? '\t' : c);
                        pos += 2;
                        continue;
                    }
                    sb.Append(t[pos++]);
                }
                pos++;
            }
            else
            {
                while (pos < t.Length && !char.IsWhiteSpace(t[pos]) && t[pos] != '{' && t[pos] != '}') sb.Append(t[pos++]);
            }
            return sb.ToString();
        }

        public static string Write(VdfNode root)
        {
            var sb = new StringBuilder();
            WriteBody(sb, root, 0);
            return sb.ToString();
        }

        static void WriteBody(StringBuilder sb, VdfNode map, int depth)
        {
            string ind = new string('\t', depth);
            foreach (var kv in map.Children)
            {
                if (kv.Value.Type == VdfNode.Kind.Map)
                {
                    sb.Append(ind).Append('"').Append(Esc(kv.Key)).Append("\"\n");
                    sb.Append(ind).Append("{\n");
                    WriteBody(sb, kv.Value, depth + 1);
                    sb.Append(ind).Append("}\n");
                }
                else
                {
                    sb.Append(ind).Append('"').Append(Esc(kv.Key)).Append("\"\t\t\"").Append(Esc(kv.Value.Str ?? kv.Value.Int.ToString())).Append("\"\n");
                }
            }
        }

        static string Esc(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
