using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace sfall_asm
{
    class Program
    {
        const int PROCESS_VM_OPERATION = 0x08;
        const int PROCESS_VM_READ = 0x10;
        const int PROCESS_VM_WRITE = 0x20;
        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr OpenProcess(
                    int dwDesiredAccess,
                    IntPtr bInheritHandle,
                    IntPtr dwProcessId
                    );

        [DllImport("kernel32.dll")]
        public static extern int ReadProcessMemory(
            IntPtr hProcess,
            int lpBase,
            ref byte lpBuffer,
            int nSize,
            int lpNumberOfBytesRead
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(
          IntPtr hProcess,
          IntPtr lpBaseAddress,
          byte[] lpBuffer,
          Int32 nSize,
          out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
         static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress,
        UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        enum RunMode
        {
            Macro,
            Procedure,
            Memory
        }

        class MemoryPatch
        {
            public byte[] data;
            public int offset;
        }

        class SSLCode
        {
            public string Name = "";
            public List<string> Info = new List<string>();
            public bool Pack = true;
            public bool Lower = true;

            public List<Tuple<string, string>> Lines = new List<Tuple<string, string>>();

            public int MaxCodeLength { get; protected set; } = 0;
            public int MaxCommentLength { get; protected set; } = 0;

            public void Add(string code, string comment = "")
            {
                Lines.Add( Tuple.Create( code, comment ));

                // keep length of longest code and comment, for shiny formatting
                if(code.Length > MaxCodeLength)
                    MaxCodeLength = code.Length;
                if(comment.Length > MaxCommentLength)
                    MaxCommentLength = comment.Length;
            }
        }

        static void Main(string[] args)
        {
            // force english language, for exceptions
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Threading.Thread.CurrentThread.CurrentCulture;

            if (args.Length == 0)
            {
                Console.WriteLine(System.AppDomain.CurrentDomain.FriendlyName + " [asm_patch] <options...>");
                Console.WriteLine();
                Console.WriteLine("RUN MODE");
                Console.WriteLine("\t--macro       Generate patch file as preprocessor macro (default)");
                Console.WriteLine("\t--procedure   Generate patch file as inline procedure");
                Console.WriteLine("\t--memory      Write the code directly into Fallout2.exe");
                Console.WriteLine();
                Console.WriteLine("SSL GENERATION");
                Console.WriteLine("\t--no-pack     Only write_byte() will be used");
                Console.WriteLine("\t--no-lower    Hex values won't be lowercased");
                Console.WriteLine();

                return;
            }

            SSLCode ssl = new SSLCode();
            RunMode runMode = RunMode.Macro;
            if(args.Length>1)
            {
                foreach (var a in args)
                {
                    // run mode
                    if(a == "--macro")
                        runMode = RunMode.Macro;
                    else if(a== "--procedure")
                        runMode = RunMode.Procedure;
                    else if (a == "--memory")
                        runMode = RunMode.Memory;
                    // ssl generation
                    else if(a == "--no-pack")
                        ssl.Pack = false;
                    else if(a == "--no-lower")
                        ssl.Lower = false;
                }
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine(args[0] + " doesn't exist.");
                return;
            }

            var lines = new List<string>();
            try
            {
                lines.AddRange(File.ReadAllLines(args[0]));
            }
            catch (Exception ex) { 
                Console.WriteLine("Unable to open file: " + ex.Message);
                Environment.Exit(1);
            };


            Process fallout2=null;
            if(runMode == RunMode.Memory)
            {
                var pids = Process.GetProcessesByName("Fallout2");
                if (pids.Length == 0)
                {
                    Console.WriteLine("Unable to find fallout2.exe");
                    return;
                }
                fallout2 = pids[0];
            }

            var memorybytes = new List<MemoryPatch>();
            var lastOffset = 0;

            Regex reMeta = new Regex(@"^//![\t ]+([A-Za-z0-9]+)[\t ]+(.+)$");
            Regex reInfo = new Regex(@"^///[\t ]+(.+)$");

            foreach (var line in lines)
            {
                Match matchMeta = reMeta.Match(line);
                if(matchMeta.Success)
                {
                    string var = matchMeta.Groups[1].Value.Trim().ToLower();
                    string val = matchMeta.Groups[2].Value.Trim();

                    if( var == "name")
                        ssl.Name = val;

                    continue;
                }

                Match matchInfo = reInfo.Match(line);
                if(matchInfo.Success)
                {
                    ssl.Info.Add(matchInfo.Groups[1].Value.Trim());

                    continue;
                }

                if (line.Length >= 2 && line[0] == '/' && line[1] == '/')
                    continue;

                if (!line.Contains('|'))
                    continue;

                var spl = line.Split('|');
                if (spl.Length < 3)
                    continue;

                // extract address, in case first column contains label
                spl[0] = Regex.Match(spl[0], "^[\t ]*[A-Fa-f0-9]+").Value;
                if(spl[0].Length == 0)
                    continue;

                var offset = Convert.ToInt32(spl[0].Trim(), 16);
                if (offset == 0)
                    offset = lastOffset;

                var bytes = spl[1].Replace(" ", "");
                for (var i = 0; i < bytes.Length; i += 2)
                {
                    if (runMode == RunMode.Memory)
                    {
                        var by = Convert.ToByte($"{bytes[i]}{bytes[i + 1]}", 16);
                        memorybytes.Add(new MemoryPatch()
                        {
                            data = new byte[] { by },
                            offset = offset
                        });
                    }
                    else
                    {
                        string write = "", offsetString = "0x" + offset.ToString("x"), bytesString = "0x";
                        int writeSize = 0;

                        // pack
                        if( ssl.Pack && i + 8 <= bytes.Length )
                        {
                            write = "int";
                            bytesString += $"{bytes[i + 6]}{bytes[i + 7]}{bytes[i + 4]}{bytes[i + 5]}{bytes[i + 2]}{bytes[i + 3]}{bytes[i]}{bytes[i + 1]}";
                            writeSize = 4;
                        }
                        else if( ssl.Pack && i + 4 <= bytes.Length )
                        {
                            write = "short";
                            bytesString += $"{bytes[i + 2]}{bytes[i + 3]}{bytes[i]}{bytes[i + 1]}";
                            writeSize = 2;
                        }
                        else
                        {
                            write = "byte";
                            bytesString += $"{bytes[i]}{bytes[i + 1]}";
                            writeSize = 1;
                        }

                        if(ssl.Lower)
                        {
                            offsetString = offsetString.ToLower();
                            bytesString = bytesString.ToLower();
                        }
                        else
                        {
                            offsetString = offsetString.ToUpper();
                            bytesString = bytesString.ToUpper();
                        }

                        // restore lowercased "x", even with --no-lower option
                        offsetString = offsetString.Replace("X", "x");
                        bytesString = bytesString.Replace("X", "x");

                        ssl.Add( $"write_{write.PadRight((ssl.Pack ? 5 : 4))}({offsetString}, {bytesString});", i == 0 ? spl[2].Trim() : "");

                        i += writeSize * 2 - 2;
                        offset += writeSize - 1;
                    }

                    offset++;
                    lastOffset = offset;
                }
            }

            if (runMode == RunMode.Memory)
            {
                var ptr = OpenProcess(0x1F0FFF, IntPtr.Zero, new IntPtr(fallout2.Id));
                foreach (var patch in memorybytes)
                {
                    WriteProcessMemory(ptr, (IntPtr)patch.offset, patch.data, patch.data.Length, out IntPtr _);
                }
            }
            else
            {
                foreach(string info in ssl.Info)
                {
                    Console.WriteLine($"// {info}");
                }

                string prefix = "", suffix = "";
                if(runMode == RunMode.Macro)
                {
                    prefix = new string(' ', 15);
                    suffix = "\\";

                    Console.WriteLine( $"#define VOODOO_{(ssl.Name.Length > 0 ? ssl.Name : "")} \\");
                }
                else if(runMode == RunMode.Procedure)
                {
                    prefix = new string(' ', 3);

                    Console.WriteLine($"inline procedure VOODOO_{(ssl.Name.Length > 0 ? ssl.Name : "")}");
                    Console.WriteLine("begin");
                }

                var lastLine = ssl.Lines[ssl.Lines.Count - 1];
                foreach(var line in ssl.Lines)
                {
                    string middle = " ", code = line.Item1, comment = line.Item2;
                    bool last = line == lastLine;

                    // remove ; from last line of marco
                    if(runMode == RunMode.Macro && last)
                    {
                        code = Regex.Match(code, "^.+\\)").Value;
                        suffix = "";
                    }

                    // align all comments to same position
                    if(comment.Length == 0)
                    {
                        if(runMode == RunMode.Macro)
                        {
                            if(!last)
                            {
                                middle = "".PadLeft((ssl.MaxCodeLength - code.Length) + 1);
                                comment = "".PadLeft(ssl.MaxCommentLength + 7);
                            }
                            else
                                middle = "";
                        }
                        else if(runMode == RunMode.Procedure)
                            middle = "";
                    }
                    else
                    {
                        middle = "".PadLeft((ssl.MaxCodeLength - code.Length) + 1);

                        if(runMode == RunMode.Macro)
                        {
                            comment = "/* " + comment + " */";
                            if(!last)
                                comment = comment.PadRight(ssl.MaxCommentLength + 7);
                        }
                        else if(runMode == RunMode.Procedure)
                            comment = "// " + comment;
                    }

                    // debug
                    // const string eol = "\\n";
                    // Console.WriteLine( $"[{prefix}][{code}][{middle}][{comment}][{suffix}]{eol}");

                    Console.WriteLine( $"{prefix}{code}{middle}{comment}{suffix}");
                }

                if(runMode == RunMode.Procedure)
                    Console.WriteLine("end");
            }
        }
    }
}
