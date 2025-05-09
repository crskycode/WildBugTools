using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptTool
{
    internal static class BinaryWriterExtensions
    {
        public static void WriteByte(this BinaryWriter writer, byte value)
        {
            writer.Write(value);
        }

        public static void WriteNullTerminatedString(this BinaryWriter writer, string s, Encoding encoding)
        {
            var bytes = encoding.GetBytes(s);
            writer.Write(bytes);
            writer.Write((byte)0);
        }
    }
}
