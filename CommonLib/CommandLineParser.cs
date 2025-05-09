using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonLib
{
    public class CommandLineParser
    {
        public static Dictionary<string, string> ParseArguments(string[] args)
        {
            var parsedArgs = new Dictionary<string, string>();

            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith('-'))
                {
                    throw new ArgumentException($"Argument {args[i]} does not start with '-'.");
                }

                var key = args[i];
                var value = string.Empty;

                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    value = args[++i];
                }

                if (parsedArgs.ContainsKey(key))
                {
                    throw new ArgumentException($"Duplicate argument '{key}' found.");
                }

                parsedArgs[key] = value;
            }

            return parsedArgs;
        }

        public static void EnsureArguments(Dictionary<string, string> args, params string[] requiredArgs)
        {
            foreach (var arg in requiredArgs)
            {
                if (!args.ContainsKey(arg))
                {
                    throw new ArgumentException($"Missing argument '{arg}'.");
                }
            }
        }
    }
}
