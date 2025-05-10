using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonLib
{
    public static class BinaryReaderExtensions
    {
        public static string ReadNullTerminatedString(this BinaryReader reader, Encoding encoding)
        {
            var buffer = new List<byte>(256);

            for (var b = reader.ReadByte(); b != 0; b = reader.ReadByte())
            {
                buffer.Add(b);
            }

            if (buffer.Count == 0)
            {
                return string.Empty;
            }

            return encoding.GetString(buffer.ToArray());
        }
    }
}
