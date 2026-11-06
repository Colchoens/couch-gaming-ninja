using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace CouchGamingNinja
{
    /// <summary>Tiny helpers over the JSON parser that ships with .NET Framework.</summary>
    public static class Json
    {
        static JavaScriptSerializer Serializer()
        {
            var s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            return s;
        }

        public static object Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            int start = IndexOfJsonStart(text);
            if (start < 0) return null;
            return Serializer().DeserializeObject(text.Substring(start));
        }

        public static string Serialize(object value)
        {
            return Serializer().Serialize(value);
        }

        // Legendary may print notices before the JSON; skip to the first { or [.
        static int IndexOfJsonStart(string text)
        {
            int a = text.IndexOf('{');
            int b = text.IndexOf('[');
            if (a < 0) return b;
            if (b < 0) return a;
            return Math.Min(a, b);
        }

        public static IDictionary<string, object> Obj(object o)
        {
            return o as IDictionary<string, object>;
        }

        public static IEnumerable<object> Arr(object o)
        {
            var list = o as IEnumerable;
            if (list == null || o is string) yield break;
            foreach (var item in list) yield return item;
        }

        public static string Str(IDictionary<string, object> o, string key)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v) || v == null) return null;
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
