using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace sfall_asm
{
    class Program
    {
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

            public List<(string, string)> Lines = new List<(string, string)>();

            public int MaxCodeLength { get; protected set; } = 0;
            public int MaxCommentLength { get; protected set; } = 0;
            public bool rfall = false;

            public void Add(string code, string comment = "")
            {
                Lines.Add((code, comment));

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
                Console.WriteLine("\t--rfall       Force using r_write_*() functions");
                Console.WriteLine();

                return;
            }

            RunMode runMode = RunMode.Macro;
            SSLCode ssl = new SSLCode();
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
                    else if(a == "--rfall")
                        ssl.rfall = true;
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
                    Console.WriteLine("Unable to find Fallout2.exe process");
                    return;
                }
                fallout2 = pids[0];
            }

            var memorybytes = new List<MemoryPatch>();
            var lastOffset = 0;

            var reMeta = new Regex(@"^//![\t ]+([A-Za-z0-9]+)[\t ]+(.+)$");
            var reInfo = new Regex(@"^///[\t ]+(.+)$");

            foreach (var line in lines)
            {
                // find additional patch configuration
                Match matchMeta = reMeta.Match(line);
                if(matchMeta.Success)
                {
                    string var = matchMeta.Groups[1].Value.Trim().ToLower();
                    string val = matchMeta.Groups[2].Value.Trim();

                    if(var == "name")
                        ssl.Name = val;

                    continue;
                }

                // find public comments, included in generated macro/procedure code
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
                        var @byte = Convert.ToByte($"{bytes[i]}{bytes[i + 1]}", 16);
                        memorybytes.Add(new MemoryPatch()
                        {
                            data = new byte[] { @byte },
                            offset = offset
                        });
                    }
                    else
                    {
                        string write = "write_", offsetString = offset.ToString("x"), bytesString = "";
                        int writeSize = 0;

                        if(ssl.Pack && i + 8 <= bytes.Length)
                        {
                            write += "int  ";
                            writeSize = 4;
                        }
                        else if(ssl.Pack && i + 4 <= bytes.Length)
                        {
                            write += "short";
                            writeSize = 2;
                        }
                        else
                        {
                            write += "byte";
                            writeSize = 1;

                            if(ssl.Pack)
                                write += " ";
                        }

                        for(int w=0, b=i; w<writeSize; w++)
                        {
                            bytesString = $"{bytes[b++]}{bytes[b++]}" + bytesString;
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

                        ssl.Add($"{write}(0x{offsetString}, 0x{bytesString});", i == 0 ? spl[2].Trim() : "");

                        // vanilla sfall cannot write outside Fallout2.exe memory currently,
                        // see sfall/Modules/Scripting/Handlers/Memory.cpp (START_VALID_ADDR, END_VALID_ADDR)
                        // after preparing all lines, code is tweaked to use less restricted sfall-rotators implementation
                        if(offset < 0x410000 || offset > 0x6B403F)
                            ssl.rfall = true;

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

                    Console.WriteLine($"#define VOODOO_{(ssl.Name.Length > 0 ? ssl.Name : "")} \\");
                }
                else if(runMode == RunMode.Procedure)
                {
                    prefix = new string(' ', 3); // default SfallEditor setting... yeah. i know.

                    Console.WriteLine($"inline procedure VOODOO_{(ssl.Name.Length > 0 ? ssl.Name : "")}");
                    Console.WriteLine("begin");
                }

                if(ssl.rfall)
                    Console.WriteLine("// sfall-rotators required");

                var lastLine = ssl.Lines[ssl.Lines.Count - 1];
                int maxCodeLengthTweak = ssl.rfall ? 2 : 0; // for correct padding
                foreach(var line in ssl.Lines)
                {
                    string middle = "", code = line.Item1, comment = line.Item2;
                    bool last = line == lastLine;

                    // add r_ prefix to ALL lines if at least one write uses sfall-rotators function or --rfall is used
                    // in first case it's technically not needed to use r_write_* if other address(es) are inside sfall limits,
                    // but mixing limited and non-limited writing can make macro/procedure useless and/or dangerous
                    if(ssl.rfall)
                        code = "r_" + code;

                    // remove ; and \ from last line of marco
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
                                middle = "".PadLeft((ssl.MaxCodeLength + maxCodeLengthTweak - code.Length) + 1);
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
                        middle = "".PadLeft((ssl.MaxCodeLength + maxCodeLengthTweak - code.Length) + 1);

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
                    // Console.WriteLine($"[{prefix}][{code}][{middle}][{comment}][{suffix}]{eol}");

                    Console.WriteLine($"{prefix}{code}{middle}{comment}{suffix}");
                }

                if(runMode == RunMode.Procedure)
                    Console.WriteLine("end");
            }
        }
    }
}
