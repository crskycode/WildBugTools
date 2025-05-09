using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptTool
{
    internal static class StreamExtensions
    {
        public static void AlignPosition(this Stream stream, int alignment)
        {
            stream.Position = (stream.Position + (alignment - 1)) & ~(alignment - 1);
        }
    }
}
