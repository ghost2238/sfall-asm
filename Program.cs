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
            Procedure,  // Procedure (inline and normal)
            Memory      // Write the code directly into a running instance of Fallout2.exe
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
                Console.WriteLine("\t--update-file     Apply changes to given file");
                Console.WriteLine("\t--malloc          Use dynamically allocated memory in patches where requested");
                Console.WriteLine();
                Console.WriteLine("GENERATORS");
                Console.WriteLine("\t--sfall-assert    Generate a patch to assert that code is correct");
                Console.WriteLine();
                Console.WriteLine("PATCH VARIABLES");
                Console.WriteLine("\t--memory-args     Set memory variables");
                Console.WriteLine();
                Console.WriteLine("DEBUGGING");
                Console.WriteLine("\t-r                Console.ReadKey() on exit");
                Console.WriteLine();
                Console.WriteLine("ERROR HANDLING");
                Console.WriteLine("\t--strict          Use strict error handling");

                return;
            }

            bool readKey = false;
            bool runEngine = true;
            var engine = new PatchEngine();

            foreach (var a in args)
            {
                // run mode
                if (a == "--macro")
                    engine.runMode = RunMode.Macro;
                else if (a == "--procedure")
                    engine.runMode = RunMode.Procedure;
                else if (a == "--memory")
                    engine.runMode = RunMode.Memory;
                // ssl generation
                else if (a == "--no-pack")
                    engine.protossl.Pack = false;
                else if (a == "--no-lower")
                    engine.protossl.Lower = false;
                else if (a == "--no-macro-guard")
                    engine.protossl.MacroGuard = false;
                else if (a == "--malloc")
                    engine.protossl.Malloc = true;
                else if (a == "-r")
                    readKey = true;
                else if (a == "-strict" || a == "--strict") // -strict left for old/forgotten scripts, "for now"
                    Error.Strict = true;
                else if (a.StartsWith("--memory-args="))
                    engine.ParseMemoryArgs(a);
                else if (a.StartsWith("--sfall-assert=")) // --sfall-assert=name,hook_address,bytes
                {
                    Asserts.Sfall.ParseAndRunAssertArgs(a);
                    runEngine = false;
                }
                else if (a.StartsWith("--update-file="))
                {
                    string filename = a.Replace("--update-file=", "");
                    engine.currentFilename = filename;
                    engine.updateFile.SetData(filename, engine.SafeReadAllLines(filename));
                }
                else
                {
                    if (Directory.Exists(a))
                        Directory.GetFiles(a, "*.asm").OrderBy(x => x).ToList().ForEach(x => engine.AddPatch(x));
                    else
                        engine.AddPatch(a);
                }
            }

            if(runEngine)
                engine.Run();

            if (readKey)
                Console.ReadKey();  
        }
    }
}
