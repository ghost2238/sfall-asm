using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace sfall_asm
{
    public class Program
    {
        public enum RunMode
        {
            Macro,      // Preprocessor macro
            Procedure,  // Inline procedure
            Memory      // Write the code directly into a running instance of Fallout2.exe
        }

        public enum ParseMode
        {
            ASM,  // Assembler byte code.
            SSL,  // Fallout 2 script code.
            Macro // sfall-asm macros.
        }

        public static class ASM
        {
            // Swap from big-endian to little-endian (or back, if already swapped).
            public static int SwapEndian(int num)
            {
                byte[] end = BitConverter.GetBytes(num).Reverse().ToArray();
                return BitConverter.ToInt32(end, 0);
            }

            public static short SwapEndian(short num)
            {
                byte[] end = BitConverter.GetBytes(num).Reverse().ToArray();
                return BitConverter.ToInt16(end, 0);
            }

            // Calculate the jump distance for relative jump/call.
            public static int CalculateRelJump32(int from, int to) => SwapEndian(to - from - 5);
        }

        // Used to resolve [some_var] in the address field or in relative instructions.
        public class MemoryArgs
        {
            private Dictionary<string, int> vars = new Dictionary<string, int>();
            public int this[string idx]
            {
                get => vars[idx];
                set { vars[idx] = value; }
            }

            public void FromArgString(string arg)
            {
                var allVars = arg.Split(',');
                foreach (var aVar in allVars)
                {
                    var keyVal = aVar.Replace(" ", "").Split('=');
                    if (keyVal.Length < 2)
                        continue;
                    var var = keyVal[0];
                    var val = keyVal[1];
                    int converted = 0;
                    if (val == "")
                    {
                        Console.WriteLine($"The value for {var} can't be empty, it needs to be a valid hex memory address.");
                        Environment.Exit(1);
                    }

                    try
                    {
                        converted = Convert.ToInt32(val, 16);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"{val} is not a valid hex memory address value for the variable {var} given in --memory-args.");
                        Environment.Exit(1);
                    }

                    vars[var] = converted;
                }
            }

            private void MemoryArgError(string var)
            {
                Console.WriteLine($"Unable to resolve the variable [{var}], did you specify the correct --memory-args?");
                Environment.Exit(1);
            }

            // Resolves a memory address identifier which uses the [identifier] syntax.
            // There are two variants:
            // 1. Address literals, useful for specifying the absolute address next to instructions relying on relative addresses.
            // 2. Variables, which are set via the CLI argument --memory-args.
            public int ResolveAddress(string str, out string resolvedLiteral)
            {
                var startIdx = str.IndexOf('[');
                var endIdx = str.IndexOf(']');
                var literal = str.Substring(startIdx + 1, endIdx - startIdx - 1);
                resolvedLiteral = literal;
                if (literal[0] == '0' && literal[1] == 'x')
                {
                    return Convert.ToInt32(literal, 16);
                }
                else
                {
                    if (!IsDefined(literal))
                        MemoryArgError(literal);
                    return vars[literal];
                }
            }

            public bool IsDefined(string var) => vars.ContainsKey(var);
        }

        static List<string> SafeReadAllLines(string file)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine(file + " doesn't exist.");
                return null;
            }
            try
            {
                return File.ReadAllLines(file).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to open file: " + ex.Message);
                Environment.Exit(1);
                return null;
            };
        }

        static void Main(string[] args)
        {
            // Force english language, for exceptions
            System.Threading.Thread.CurrentThread.CurrentCulture   = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Threading.Thread.CurrentThread.CurrentCulture;

            if (args.Length == 0)
            {
                Console.WriteLine(AppDomain.CurrentDomain.FriendlyName + " [asm_patch] <options...>");
                Console.WriteLine();
                Console.WriteLine("RUN MODE");
                Console.WriteLine("\t--macro           Generate patch file as preprocessor macro (default)");
                Console.WriteLine("\t--procedure       Generate patch file as inline procedure");
                Console.WriteLine("\t--memory          Write the code directly into Fallout2.exe");
                Console.WriteLine();
                Console.WriteLine("SSL GENERATION");
                Console.WriteLine("\t--no-lower        Hex values won't be lowercased");
                Console.WriteLine("\t--no-macro-guard  Macros won't be guarded with begin/end");
                Console.WriteLine("\t--no-pack         Force using write_byte() function only");
                Console.WriteLine("\t--rfall           Force using r_write_*() functions");
                Console.WriteLine();
                Console.WriteLine("PATCH VARIABLES");
                Console.WriteLine("\t--memory-args     Set memory variables");
                Console.WriteLine();
                Console.WriteLine("DEBUGGING");
                Console.WriteLine("\t-r                Console.ReadKey() on exit");

                return;
            }

            
            var memoryArgs = new MemoryArgs();
            bool readKey = false;
            RunMode runMode = RunMode.Macro;
            SSLCode ssl = new SSLCode("VOODOO");
            if(args.Length>1)
            {
                foreach (var a in args)
                {
                    // run mode
                    if (a == "--macro")
                        runMode = RunMode.Macro;
                    else if (a == "--procedure")
                        runMode = RunMode.Procedure;
                    else if (a == "--memory")
                        runMode = RunMode.Memory;
                    // ssl generation
                    else if (a == "--no-pack")
                        ssl.Pack = false;
                    else if (a == "--no-lower")
                        ssl.Lower = false;
                    else if (a == "--no-macro-guard")
                        ssl.MacroGuard = false;
                    else if (a == "--rfall")
                        ssl.RFall = true;
                    else if (a == "-r")
                        readKey = true;
                    else if (a.StartsWith("--memory-args="))
                    {
                        memoryArgs.FromArgString(a.Replace("--memory-args=", ""));
                    }
                    else
                    {
                        if (a != args[0])
                        {
                            Console.WriteLine($"'{a}' is not a valid argument.");
                            Environment.Exit(1);
                        }
                    }
                }
            }

            var lines = SafeReadAllLines(args[0]);
            if (lines == null)
                Environment.Exit(1);

            var patch = new Patch(lines, runMode, ssl, memoryArgs);
            patch.Run();

            if (readKey)
                Console.ReadKey();
        }
    }
}
