using CommonLib;
using System;
using System.IO;
using System.Text;

namespace ArcTool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("WildBug Archive Tool");
                Console.WriteLine("  created by Crsky");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("  Extract : ArcTool -e -in [input.wbp] -out [output] -cp [codepage]");
                Console.WriteLine("  Create  : ArcTool -c -in [folder] -out [output.wbp] -cp [codepage]");
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");

                Environment.ExitCode = 1;
                Console.ReadKey();

                return;
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var parsedArgs = CommandLineParser.ParseArguments(args);

            CommandLineParser.EnsureArguments(parsedArgs, "-in", "-out", "-cp");

            var inputPath = Path.GetFullPath(parsedArgs["-in"]);
            var outputPath = Path.GetFullPath(parsedArgs["-out"]);
            var encoding = Encoding.GetEncoding(parsedArgs["-cp"]);

            if (parsedArgs.ContainsKey("-e"))
            {
                Arc.Extract(inputPath, outputPath, encoding);
                return;
            }

            if (parsedArgs.ContainsKey("-c"))
            {
                Arc.Create(outputPath, inputPath, encoding);
                return;
            }
        }
    }
}
