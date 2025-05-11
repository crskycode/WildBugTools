using CommonLib;
using System;
using System.IO;
using System.Text;

namespace ImgTool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("WildBug Image Tool");
                Console.WriteLine("  created by Crsky");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("  Extract : ImgTool -e -in [input.wbm] -out [output.png]");
                Console.WriteLine("  Create  : ImgTool -c -in [input.wbm] -img [input.png] -out [output.wbm]");
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");

                Environment.ExitCode = 1;
                Console.ReadKey();

                return;
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var parsedArgs = CommandLineParser.ParseArguments(args);

            CommandLineParser.EnsureArguments(parsedArgs, "-in", "-out");

            var inputPath = Path.GetFullPath(parsedArgs["-in"]);
            var outputPath = Path.GetFullPath(parsedArgs["-out"]);

            if (parsedArgs.ContainsKey("-e"))
            {
                WBM.Extract(inputPath, outputPath);
                return;
            }

            if (parsedArgs.ContainsKey("-c"))
            {
                CommandLineParser.EnsureArguments(parsedArgs, "-img");
                var imagePath = Path.GetFullPath(parsedArgs["-img"]);
                WBM.Create(inputPath, imagePath, outputPath);
                return;
            }
        }
    }
}
