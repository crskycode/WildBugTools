using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptTool
{
    internal static class StringExtensions
    {
        public static string Escape(this string s)
        {
            return s.Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        public static string Unescape(this string s)
        {
            return s.Replace("\\n", "\n")
                .Replace("\\r", "\r");
        }
    }
}
