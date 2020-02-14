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
            // NOTE: enum names starting with "write_" are used as-is in generated code
            protected enum LineType
            {
                write_byte,
                write_short,
                write_int,
                comment
            };

            protected class Line
            {
                public LineType Type;
                public int Address;
                public int Value;
                public string Comment;

                public bool RFall;
                public bool HRP;

                public string HexFormat;

                public string FunctionString => (RFall ? "r_" : "") + Enum.GetName(typeof(LineType), Type).ToLower();
                public string AddressString => (HRP ? "r_hrp_offset(" : "") + "0x" + Address.ToString(HexFormat) + (HRP ? ")" : "");
                public string ValueString
                {
                    get
                    {
                        string result = "";

                        if(Type == LineType.write_byte)
                            result = ((byte)Value).ToString($"{HexFormat}2");
                        else if(Type == LineType.write_short)
                            result = ((short)Value).ToString($"{HexFormat}4");
                        else if(Type == LineType.write_int)
                            result = Value.ToString($"{HexFormat}8");

                        if(result.Length > 0)
                            result = $"0x{result}";

                        return result;
                    }
                }

                public Line(LineType type, int address, int value, string comment = "")
                {
                    Type = type;
                    Address = address;
                    Value = value;
                    Comment = comment;

                    RFall = HRP = false;
                }
            };

            public readonly string NamePrefix;
            public string Name = "";
            public bool Lower = true;
            public bool Pack = true;
            public bool RFall = false;

            protected List<string> Info = new List<string>();
            protected List<Line> Lines = new List<Line>();
            protected Line LastLine;
            protected Line LastWriteLine;

            public SSLCode(string namePrefix = "")
            {
                NamePrefix = namePrefix;
            }

            public void AddInfo(string info)
            {
                Info.Add(info);
            }

            public void AddWrite(int size, int address, int value, string comment = "")
            {
                LineType type;

                if(size == 4)
                    type = LineType.write_int;
                else if(size == 2)
                    type = LineType.write_short;
                else if(size == 1)
                    type = LineType.write_byte;
                else
                    throw new ArgumentOutOfRangeException(nameof(size));

                Lines.Add(new Line(type, address, value, comment));
                LastWriteLine = LastLine = Lines.Last();

                // vanilla sfall cannot write outside Fallout2.exe memory currently,
                // after preparing all lines, code is tweaked to use less restricted rfall implementation
                // see sfall/Modules/Scripting/Handlers/Memory.cpp -- START_VALID_ADDR, END_VALID_ADDR
                if(address < 0x410000 || address > 0x6B403F)
                    LastLine.RFall = true;

                // f2_res.dll base address might change in some conditions
                // make sure macro/procedure is writing at correct position by tweaking address same way as sfall does
                // see sfall/main.cpp -- HRPAddress()
                if(address >= 0x10000000 && address <= 0x10077000)
                    LastLine.RFall = LastLine.HRP = true;

                // add r_ prefix to ALL lines if at least one write uses rfall function or --rfall is used
                // in first case it's technically not needed to use r_write_* if other address(es) are inside sfall limits,
                // but mixing limited and non-limited writing can make macro/procedure useless and/or dangerous
                if(!LastLine.RFall && this.RFall)
                    LastLine.RFall = true;
                else if(LastLine.RFall && !this.RFall)
                {
                    foreach(Line line in Lines)
                    {
                        line.RFall = true;
                    }

                    this.RFall = true;
                }
            }

            public void AddComment(string comment)
            {
                Lines.Add(new Line(LineType.comment, 0, 0, comment));

                LastLine = Lines.Last();
            }

            protected void PreProcessNOP()
            {
                bool isNOP8(int idx) => Lines[idx].Type == LineType.write_byte && Lines[idx].Value == 0x90 && Lines[idx].Comment == "nop";
                bool isNOP16(int idx) => Lines[idx].Type == LineType.write_short && Lines[idx].Value == 0x9090  && Lines[idx].Comment == "nop";
                bool isNOP32(int idx) => Lines[idx].Type == LineType.write_int && (uint)Lines[idx].Value == 0x90909090  && Lines[idx].Comment == "nop";

                for(int l=0, len=Lines.Count; l<len; l++)
                {
                    // lower amount of lines generated for duplicated NOP instructions
                    if(Pack)
                    {
                        // byte, byte, byte, byte -> int
                        if(l + 4 <= len && isNOP8(l) && isNOP8(l+1) && isNOP8(l+2) && isNOP8(l+3))
                        {
                            Lines[l].Type = LineType.write_int;
                            Lines[l].Value = unchecked((int)0x90909090);

                            Lines.RemoveAt(l+1);
                            Lines.RemoveAt(l+1);
                            Lines.RemoveAt(l+1);

                            len -= 3;
                        }
                        // short, short -> int
                        else if(l + 2 <= len && isNOP16(l) && isNOP16(l+1))
                        {
                            Lines[l].Type = LineType.write_int;
                            Lines[l].Value = unchecked((int)0x90909090);

                            Lines.RemoveAt(l+1);

                            len--;
                        }
                        // byte, byte -> short
                        else if(l + 2 <= len && isNOP8(l) && isNOP8(l+1))
                        {
                            Lines[l].Type = LineType.write_short;
                            Lines[l].Value = 0x9090;

                            Lines.RemoveAt(l+1);

                            len--;
                        }
                    }

                    if(isNOP32(l))
                        Lines[l].Comment = "nop; nop; nop; nop";
                    else if(isNOP16(l))
                        Lines[l].Comment = "nop; nop";
                }
            }

            public void PreProcess()
            {
                PreProcessNOP();
            }

            public string GetName()
            {
                string result = "";

                if(NamePrefix.Length > 0)
                    result += NamePrefix + "_";

                if(Name.Length > 0)
                    result += Name;

                return result;
            }

            public List<string> GetInfo()
            {
                List<string> result = new List<string>();

                foreach(string info in Info)
                {
                    result.Add($"// {info}");
                }

                bool hrp = false, rfall = false;
                foreach(var line in Lines)
                {
                    if(!hrp && line.HRP)
                    {
                        hrp = true;
                        result.Add("// hrp required");
                    }

                    if(!rfall && line.RFall)
                    {
                        rfall = true;
                        result.Add("// rfall required");
                    }

                    if(rfall && hrp)
                        break;
                }

                return result;
            }

            public List<string> GetBody(RunMode mode)
            {
                if(mode != RunMode.Macro && mode != RunMode.Procedure)
                    throw new InvalidOperationException("You're kidding, right?");

                List<string> result = new List<string>();
                string resultmp;

                string prefix = "";
                if(mode == RunMode.Macro)
                {
                    prefix = new string(' ', 8 + (NamePrefix.Length > 0 ? NamePrefix.Length + 1 : 0));

                    result.Add($"#define {GetName()} \\");
                }
                else if(mode == RunMode.Procedure)
                {
                    prefix = new string(' ', 3); // default SFallEditor setting... yeah. i know.

                    result.Add($"inline procedure {GetName()}");
                    result.Add("begin");
                }

                // collect maximum length of each subelement
                int maxFunctionLength = 0, maxAddressLength = 0, maxValueLength = 0, maxCommentLength = 0;
                int maxRawWriteMacroLength, maxRawWriteProcedureLength = 0, maxRawCommentLength = 0;
                int maxRawLength = 0;

                foreach(Line line in Lines)
                {
                    if(line.Type == LineType.write_byte || line.Type == LineType.write_short || line.Type == LineType.write_int)
                    {
                        line.HexFormat = Lower ? "x" : "X";

                        maxFunctionLength = Math.Max(maxFunctionLength, line.FunctionString.Length);
                        maxAddressLength = Math.Max(maxAddressLength, line.AddressString.Length);
                        maxValueLength = Math.Max(maxValueLength,line.ValueString.Length);
                        maxCommentLength = Math.Max(maxCommentLength, line.Comment.Length);
                    }
                    else if(line.Type == LineType.comment)
                    {
                        maxRawCommentLength = Math.Max(maxRawCommentLength, line.Comment.Length);
                    }
                }
                //                       write               (   0x1207             ,   _   0x1337           )   ;   _   /   *   _   text               _   *   /
                maxRawWriteMacroLength = maxFunctionLength + 1 + maxAddressLength + 1 + 1 + maxValueLength + 1 + 1 + 1 + 1 + 1 + 1 + maxCommentLength + 1 + 1 + 1;
                //                           write               (   0x1207             ,   _   0x1337           )   ;   _   /   /   _   text
                maxRawWriteProcedureLength = maxFunctionLength + 1 + maxAddressLength + 1 + 1 + maxValueLength + 1 + 1 + 1 + 1 + 1 + 1 + maxCommentLength;
                //                    /   *   _   text                 _   *   /
                maxRawCommentLength = 1 + 1 + 1 + maxRawCommentLength+ 1 + 1 + 1;

                if(mode == RunMode.Macro)
                    maxRawLength = Math.Max(maxRawWriteMacroLength, maxRawCommentLength);
                else if(mode == RunMode.Procedure)
                    maxRawLength = Math.Max(maxRawWriteProcedureLength, maxRawCommentLength);

                foreach(var line in Lines)
                {
                    resultmp = prefix;

                    string comment(string value) => (mode == RunMode.Macro ? $"/* {value} */" : $"// {value}");

                    if(line.Type == LineType.write_byte || line.Type == LineType.write_short || line.Type == LineType.write_int)
                    {
                        resultmp += line.FunctionString.PadRight(maxFunctionLength);
                        resultmp += $"({line.AddressString}, ".PadRight(maxAddressLength + 3);
                        resultmp += $"{line.ValueString}){(mode == RunMode.Macro && line == LastWriteLine ? " " : ";")} ".PadRight(maxValueLength + 3);

                        if(line.Comment.Length > 0)
                            resultmp += comment(line.Comment);
                    }
                    else if(line.Type == LineType.comment)
                        resultmp += comment(line.Comment);

                    if(mode == RunMode.Macro)
                    {
                        // make sure all line types are same length before adding suffix
                        resultmp = resultmp.PadRight(maxRawLength + prefix.Length);

                        if(line != LastLine)
                            resultmp += " \\";
                    }

                    result.Add(resultmp.TrimEnd());
                }

                if(mode == RunMode.Procedure)
                    result.Add("end");

                return result;
            }

            public List<string> Get(RunMode mode)
            {
                List<string> result = new List<string>();

                result.AddRange(GetInfo());
                result.AddRange(GetBody(mode));

                return result;
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
                Console.WriteLine("\t--no-lower    Hex values won't be lowercased");
                Console.WriteLine("\t--no-pack     Force using write_byte() function only");
                Console.WriteLine("\t--rfall       Force using r_write_*() functions");
                Console.WriteLine();

                return;
            }

            RunMode runMode = RunMode.Macro;
            SSLCode ssl = new SSLCode("VOODOO");
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
                        ssl.RFall = true;
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
            catch (Exception ex)
            {
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
            bool asmStart = false;

            foreach (var line in lines)
            {
                // find additional patch configuration
                Match matchMeta = reMeta.Match(line);
                if(matchMeta.Success)
                {
                    string var = matchMeta.Groups[1].Value.Trim().ToUpper();
                    string val = matchMeta.Groups[2].Value.Trim();

                    // changes "///" behavior to include comment inside macro/procedure body
                    if(var == "ASM")
                        asmStart = true;
                    // sets macro/procedure name
                    else if(var == "NAME")
                        ssl.Name = val;

                    continue;
                }

                // find public comments, included in generated macro/procedure code
                Match matchInfo = reInfo.Match(line);
                if(matchInfo.Success)
                {
                    if(!asmStart)
                        ssl.AddInfo(matchInfo.Groups[1].Value.Trim());
                    else
                        ssl.AddComment(matchInfo.Groups[1].Value.Trim());

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

                // changes "///" behavior to include comment inside macro/procedure body
                asmStart = true;

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
                        string bytesString = "";
                        int writeSize = 0;

                        if(ssl.Pack && i + 8 <= bytes.Length)
                            writeSize = 4;
                        else if(ssl.Pack && i + 4 <= bytes.Length)
                            writeSize = 2;
                        else
                            writeSize = 1;

                        for(int w=0, b=i; w<writeSize; w++)
                        {
                            bytesString = $"{bytes[b++]}{bytes[b++]}" + bytesString;
                        }

                        ssl.AddWrite(writeSize, offset, Convert.ToInt32(bytesString, 16), i == 0 ? spl[2].Trim() : "");

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
                ssl.PreProcess();

                foreach(var line in ssl.Get(runMode))
                {
                    Console.WriteLine(line);
                }
            }
        }
    }
}
