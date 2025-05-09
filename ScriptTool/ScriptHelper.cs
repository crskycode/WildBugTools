using CommonLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptTool
{
    public class ScriptHelper
    {
        public static void Decompress(string inputPath, string outputPath)
        {
            using var reader = new WpxReader(inputPath, "EX2");

            const int scnId = 2;

            if (reader.Contains(scnId))
            {
                var data = reader.Read(scnId);
                reader.Dispose();

                File.WriteAllBytes(outputPath, data);
            }
        }
    }
}
