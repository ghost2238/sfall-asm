using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static sfall_asm.Program;

namespace sfall_asm
{
    public enum ParseMode
    {
        ASM,  // Assembler byte code.
        SSL,  // Fallout 2 script code.
        Macro // sfall-asm macros.
    }

    public enum ParseEvent
    {
        ResolvedAddressMemoryArg // A memory arg was found in the address column and was resolved.
    }

    public class MemoryArgInfo
    {
        public string name;
        public int address;
        // public string fileInfo; // file and line.
    }

    public class ParseEventInfo
    {
        public ParseEvent @event;
        public object info;

        public MemoryArgInfo GetMemoryArgInfo() => (MemoryArgInfo)info;
    }

    // Used to resolve [some_var] in the address field or in relative instructions.
    public class MemoryArgs
    {
        private readonly Dictionary<string, int> vars = new Dictionary<string, int>();

        public int this[string idx]
        {
            get => vars[idx];
            set { vars[idx] = value; }
        }

        public string this[int address] => vars.FirstOrDefault(x => x.Value == address).Key;

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
                    Error.Fatal($"The value for {var} can't be empty, it needs to be a valid hex memory address.", ErrorCodes.EmptyMemoryAddress);

                try
                {
                    converted = Convert.ToInt32(val, 16);
                }
                catch (Exception)
                {
                    Error.Fatal($"{val} is not a valid hex memory address value for the variable {var} given in --memory-args.", ErrorCodes.InvalidMemoryAddress);
                }

                vars[var] = converted;
            }
        }

        private void MemoryArgError(string var)
        {
            Error.Fatal($"Unable to resolve the variable [{var}], did you specify the correct --memory-args?",
                ErrorCodes.UnableToResolveMemoryAddress);
        }

        // Resolves a memory address identifier which uses the [identifier] syntax.
        // There are two variants:
        // 1. Address literals, useful for specifying the absolute address next to instructions relying on relative addresses.
        // 2. Variables, which are set via the CLI argument --memory-args.
        public int ResolveAddress(string str, out string resolvedLiteral)
        {
            var startIdx = str.IndexOf('[');
            var endIdx = str.IndexOf(']');
            if (startIdx == -1)
                Error.Fatal("Unable to resolve memory address, missing [", ErrorCodes.ParseErrorMemoryAddress);
            if (endIdx == -1)
                Error.Fatal("Unable to resolve memory address, missing ]", ErrorCodes.ParseErrorMemoryAddress);

            var literal = str.Substring(startIdx + 1, endIdx - startIdx - 1);
            if (literal.Length == 0)
                Error.Fatal("Memory literal is empty.", ErrorCodes.ParseErrorMemoryAddress);

            resolvedLiteral = literal;
            if (literal[0] == '0' && literal[1] == 'x')
            {
                try
                {
                    return Convert.ToInt32(literal, 16);
                }
                catch (Exception ex)
                {
                    Error.Fatal($"{literal} is not a valid hex memory literal: {ex.Message}.", ErrorCodes.InvalidMemoryAddress);
                    return -1;
                }
            }
            else
            {
                if (!IsDefined(literal))
                    MemoryArgError(literal);
                return vars[literal];
            }
        }

        public bool IsDefined(string var) => vars.ContainsKey(var);
        public bool IsDefined(int val) => vars.ContainsValue(val);
    }

    // Bytestring which can also contain memory literals.
    public class ByteString
    {
        private string str;
        public int offset;

        public string RawString { get => str; }

        public ByteString(string str)
        {
            this.str = str.Replace(" ", "").Replace(":", "");
            this.offset = 0;
        }

        public char PeekChar(int pos)
        {
            if (offset + pos >= str.Length)
                return '\0';
            return str[offset + pos];
        }
        public string ReadChars(int num)
        {
            var read = str.Substring(offset, num);
            offset += num;
            return read;
        }
        public byte ReadByte()
        {
            var b = PeekByte();
            offset += 2;
            return b;
        }
        public byte PeekByte()
        {
            if (EOF)
                return 0;

            return Convert.ToByte($"{PeekChar(0)}{PeekChar(1)}", 16);
        }
        public bool HasBytesLeft(int numBytes) => offset + numBytes * 2 <= str.Length;
        public int AsInt(int numBytes)
        {
            if (numBytes == 1)
                return (int)ReadByte();
            else if (numBytes == 2)
                return (int)ASM.SwapEndian(Convert.ToInt16(ReadChars(numBytes * 2), 16));
            else if (numBytes == 3)
                throw new Exception("Can't read 3 bytes.");
            else if (numBytes == 4)
                return ASM.SwapEndian(Convert.ToInt32(ReadChars(numBytes * 2), 16));
            else
                throw new Exception("Can't read more than 4 bytes.");
        }

        public void ResolveMemoryArg(MemoryArgs args, int currentOffset)
        {
            int addr = args.ResolveAddress(str.Substring(offset + 1), out string resolvedLiteral);
            int jumpBytes = ASM.CalculateRelJump32(currentOffset, addr);
            var litBrackets = $"[{resolvedLiteral}]";
            var jumpBytesStr = jumpBytes.ToString("x");
            // https://github.com/ghost2238/sfall-asm/issues/3#issuecomment-588108479
            // Pad the address so that it's a valid 32-bit address.
            if (jumpBytesStr.Length < 8)
                jumpBytesStr = jumpBytesStr.PadRight(8, '0');
            str = str.Replace(litBrackets, jumpBytesStr).ToUpper();

        }

        public bool EOF => offset >= str.Length;

        public string Str { get => str; set => str = value; }
    }

    class UpdateFile
    {
        public string Name { get; private set; } = "";
        public Dictionary<string,int> Tags { get; private set; } = new Dictionary<string,int>();
        public List<string> Lines { get; private set; } = new List<string>();
        public bool IsSet { get; private set; } = false;

        public readonly Regex TagRegex;
        public readonly string TagOpen;
        public readonly string TagClose;

        public UpdateFile(Regex tagRegex, string tagOpen, string tagClose)
        {
            TagRegex = tagRegex;
            TagOpen = tagOpen;
            TagClose = tagClose;
        }

        // cache file content without lines inside all tags
        // position of all tags is saved for later use
        public void SetData(string filename, List<string> lines)
        {
            UnsetData();

            Match match;
            bool tag = false;
            int lineNum = 0;

            foreach(string line in lines)
            {
                match = TagRegex.Match(line);
                if(match.Success)
                {
                    lineNum++;

                    if(match.Groups[2].Value == TagOpen)
                    {
                        if(tag)
                            Error.Fatal($"UpdateFile invalid : nested open tag", ErrorCodes.InvalidUpdateFile);

                        Tags[match.Groups[1].Value] = lineNum;
                        Lines.Add(line);
                        tag = true;
                    }
                    else if(match.Groups[2].Value == TagClose)
                    {
                        if(!tag)
                            Error.Fatal($"UpdateFile invalid : close tag without open tag", ErrorCodes.InvalidUpdateFile);

                        Lines.Add(line);
                        tag = false;
                    }
                    else
                        Error.Fatal($"UpdateFile invalid : unknown tag type", ErrorCodes.InvalidUpdateFile);
                }
                else if(!tag)
                {
                    Lines.Add(line);
                    lineNum++;
                }
            }

            if(tag)
                Error.Fatal($"UpdateFile invalid : tag not closed", ErrorCodes.InvalidUpdateFile);

            IsSet = true;
            Name = filename;
        }

        public void UnsetData()
        {
            IsSet = false;

            Name = "";
            Tags.Clear();
            Lines.Clear();
        }

        public void SetTag(string name, List<string> content)
        {
            if(!IsSet || !Tags.ContainsKey(name) || content.Count == 0)
                return;

            int lineNum = Tags[name];

            Lines.Insert(lineNum, ""); // empty line before close tag
            Lines.InsertRange(lineNum, content);
            Lines.Insert(lineNum, ""); // empty line after open tag

            // shift position of all tags after this one
            foreach(string key in Tags.Keys.ToList())
            {
                if (Tags[key] > lineNum)
                    Tags[key] += content.Count + 2;
            }
        }
    }

    class PatchEngine
    {
        private List<IASMParser> ASMParsers = new List<IASMParser>();
        private List<ISSLPreProcessor> SSLPreProcessor = new List<ISSLPreProcessor>();
        private List<string> patchFiles = new List<string>();
        private List<ParseEventInfo> parseEvents = new List<ParseEventInfo>();
        private MemoryArgs memoryArgs = new MemoryArgs();
        public RunMode runMode;
        public SSLCode protossl;
        public UpdateFile updateFile;
        public string currentFilename;
        public int currentLine;

        public PatchEngine()
        {
            runMode = RunMode.Macro;
            protossl = new SSLCode("VOODOO");
            updateFile = new UpdateFile(new Regex(@"^[\t ]*/[/]+[\t ]+sfall-asm:([A-Za-z0-9_]+)-(begin|end)[\t ]+/[/]+[\t ]*$"), "begin", "end");

            Error.GetErrorContext = () => GetErrorContext();
        }

        public string FileAndLine => $"{currentFilename}:{currentLine}";
        public string GetErrorContext()
        {
            return $"<{FileAndLine}>";
        }

        public void AddPatch(string patchFile) => patchFiles.Add(patchFile);
        public void AddSSLPreProcessor(ISSLPreProcessor processor) => SSLPreProcessor.Add(processor);
        public void AddASMParser(IASMParser parser) => ASMParsers.Add(parser);
        public bool MultiPatch => patchFiles.Count > 1;

        public void ParseMemoryArgs(string arg)
        {
            memoryArgs.FromArgString(arg.Replace("--memory-args=", ""));
        }

        public List<string> SafeReadAllLines(string file)
        {
            if (!File.Exists(file))
            {
                var msg = file + " doesn't exist.";
                if (Error.Strict)
                    Error.Fatal(msg, ErrorCodes.FileDoesntExist);
                else
                    Console.WriteLine(msg);

                return null;
            }
            try
            {
                return File.ReadAllLines(file).ToList();
            }
            catch (Exception ex)
            {
                Error.Fatal("Unable to open file: " + ex.Message, ErrorCodes.UnableToOpenFile);
                return null;
            };
        }

        public void Run()
        {
            List<string> result = new List<string>();
            List<string> defines = new List<string>();
            List<string> code = new List<string>();

            SortedDictionary<int,string> addressDict = new SortedDictionary<int,string>();
            int addressDictMax = 0;

            foreach (string patchFile in patchFiles)
            {
                this.currentFilename = Path.GetFileName(patchFile);
                SSLCode ssl = new SSLCode(protossl);

                var lines = SafeReadAllLines(patchFile);
                if (lines == null)
                    Console.Error.WriteLine($"Skipping {patchFile}");

                try
                {
                    var patch = new Patch(lines, runMode, ssl, memoryArgs, (line) => this.currentLine = line, this.ASMParsers);
                    code.AddRange(patch.Run(SSLPreProcessor));
                    this.parseEvents.AddRange(patch.parseEvents);
                }
                catch(Exception ex)
                {
                    Error.Fatal($"Unhandled exception: {ex.Message}\r\n{ex.StackTrace}", ErrorCodes.UnhandledException);
                }

                if (!MultiPatch)
                    break;

                foreach (KeyValuePair<int, int> group in ssl.GetWriteGroups())
                {
                    if (memoryArgs.IsDefined(group.Key))
                    {
                        string memoryArgName = memoryArgs[group.Key];
                        string defineName = $"{ssl.GetName()}__{memoryArgName}";

                        addressDict[-memoryArgs[memoryArgName]] = defineName; // store as negative for cheap reversed order
                        addressDictMax = Math.Max(addressDictMax, defineName.Length);

                        memoryArgs[memoryArgName] = group.Value;
                    }
                }

                if(patchFile != patchFiles.Last())
                    code.Add("");
            }

            foreach(KeyValuePair<int,string> define in addressDict)
            {
                defines.Insert(0, $"#define {define.Value.PadRight(addressDictMax)}  0x{(-define.Key).ToString("x")}");
            }

            if(updateFile.IsSet)
            {
                updateFile.SetTag(nameof(defines), defines);
                updateFile.SetTag(nameof(code), code);
            }
            else
            {
                if(defines.Count > 0)
                    defines.Add("");

                result.AddRange(defines);
                result.AddRange(code);
            }

            if(updateFile.IsSet)
                File.WriteAllLines(updateFile.Name, updateFile.Lines);
            else
                result.ForEach(line => Console.WriteLine(line));
        }
    }

    public class Patch
    {
        private List<string> lines;
        private RunMode runMode;
        private List<MemoryPatch> memoryBytes;
        private Action<int> lineReporter;
        private List<IASMParser> ASMParsers;
        public int currentOffset;
        public int lastOffset;
        public SSLCode ssl;
        public MemoryArgs memoryArgs;
        public List<ParseEventInfo> parseEvents = new List<ParseEventInfo>();

        public Patch(List<string> lines, RunMode runMode, SSLCode ssl, MemoryArgs memoryArgs, Action<int> lineReporter, List<IASMParser> asmParsers)
        {
            this.lines = lines;
            this.runMode = runMode;
            this.ssl = ssl;
            this.memoryArgs = memoryArgs;
            this.lineReporter = lineReporter;
            this.ASMParsers = asmParsers;
            this.Parse();
        }

        public void Parse()
        {
            this.memoryBytes = new List<MemoryPatch>();
            lastOffset = 0;

            var reMeta = new Regex(@"^//![\t ]+([A-Za-z0-9]+)[\t ]+(.+)$");
            var reInfo = new Regex(@"^///[\t ]+(.+)$");
            var reAddr = new Regex(@"^[\t ]*(|\$[\+\-][A-Fa-f0-9]+[\t ]+|\$[\t ]+[=]+>[\t ]+)([A-Fa-f0-9]+)");

            // Changes "///" behavior to include comment inside macro/procedure body
            bool bodyStart = false;

            Match match;
            ParseMode mode = ParseMode.ASM;
            for (int i = 0; i < lines.Count; i++)
            {
                lineReporter?.Invoke(i + 1);
                var line = lines[i];
                // Find additional patch configuration or mode switches
                match = reMeta.Match(line);
                if (match.Success)
                {
                    string var = match.Groups[1].Value.Trim().ToUpper();
                    string val = match.Groups[2].Value.Trim();

                    if (var == "NAME")
                        ssl.Name = val;
                    else if (var == "SSL")
                    {
                        mode = ParseMode.SSL;
                        bodyStart = true;
                    }
                    else if (var == "MACRO")
                    {
                        mode = ParseMode.Macro;
                        bodyStart = true;
                    }
                    else if (var == "ASM")
                    {
                        mode = ParseMode.ASM;
                        bodyStart = true;
                    }

                    continue;
                }

                // Find public comments, included in generated macro/procedure code
                match = reInfo.Match(line);
                if (match.Success)
                {
                    if (!bodyStart)
                        ssl.AddInfo(match.Groups[1].Value.Trim());
                    else
                        ssl.AddComment(match.Groups[1].Value.Trim());

                    continue;
                }

                // A comment for the reader of the .asm file.
                if (line.Length >= 2 && line[0] == '/' && line[1] == '/')
                    continue;

                if (mode == ParseMode.ASM)
                {
                    if (!line.Contains('|'))
                        continue;

                    // 0044A785 | E9 FE06FDFF   | jmp fallout2.41AE88
                    // Address  | Byte sequence | Mnemonic / comment
                    var spl = line.Split('|');
                    if (spl.Length < 3)
                        continue;

                    // Extract address, in case first column contains detailed address info and/or label
                    match = reAddr.Match(line);
                    if (match.Success)
                        spl[0] = match.Groups[2].Value;
                    else // Might be a variable also, [memory_var]
                    {
                        // Extract and resolve it
                        if (spl[0].Count(x => x == '[') == 1 && spl[0].Count(x => x == ']') == 1)
                        {
                            int address = memoryArgs.ResolveAddress(spl[0], out string resolvedVariable);
                            spl[0] = address.ToString("x");
                            this.parseEvents.Add(new ParseEventInfo()
                            {
                                @event=ParseEvent.ResolvedAddressMemoryArg,
                                info = new MemoryArgInfo()
                                {
                                    address = address,
                                    name = resolvedVariable
                                }
                            });
                        }
                    }

                    if (spl[0].Length == 0)
                        continue;

                    // changes "///" behavior to include comment inside macro/procedure body
                    bodyStart = true;

                    currentOffset = Convert.ToInt32(spl[0].Trim(), 16);
                    if (currentOffset == 0)
                        currentOffset = lastOffset;

                    var bytes = new ByteString(spl[1]);
                    bool opLine = true;
                    while (!bytes.EOF)
                    {
                        bool parserHandled = false;
                        foreach(var asmParser in this.ASMParsers)
                        {
                            parserHandled = asmParser.Process(bytes, currentOffset, this, parseEvents);
                            if (parserHandled)
                                break;
                        }
                        if (parserHandled)
                            break;

                        // Instructions which supports using absolute->rel addressing or memory variable.
                        // https://github.com/ghost2238/sfall-asm/issues/3
                        // This only works for 32-bit, if we are in 16-bit mode it won't work.
                        // But since we don't care about that at the moment it should be fine.
                        if ((bytes.PeekByte() == 0xE9) || (bytes.PeekByte() == 0xE8))
                        {
                            if (bytes.PeekChar(2) == '[')
                                bytes.ResolveMemoryArg(memoryArgs, currentOffset);
                        }

                        if (runMode == RunMode.Memory)
                        {
                            memoryBytes.Add(new MemoryPatch()
                            {
                                data = new byte[] { bytes.ReadByte() },
                                offset = currentOffset
                            });
                        }
                        else
                        {
                            int writeSize = 0;
                            if (ssl.Pack && bytes.HasBytesLeft(4))
                                writeSize = 4;
                            else if (ssl.Pack && bytes.HasBytesLeft(2))
                                writeSize = 2;
                            else
                                writeSize = 1;

                            ssl.AddWrite(writeSize, currentOffset, bytes.AsInt(writeSize), opLine ? spl[2].Trim() : "");
                            // this is to decide if comment should be written or not.
                            opLine = false;
                            currentOffset += writeSize - 1;
                        }

                        currentOffset++;
                        lastOffset = currentOffset;
                    }
                }
                else if (mode == ParseMode.SSL)
                {
                    // This needs to be more robust probably.
                    // Right now we don't really care about which context we are in.
                    // Since [] is used for arrays in SSL scripting with sfall there could be issues.
                    // Thankfully for us, SSL does not (at this moment in time) allow declaring arrays inside a function call.
                    // https://fakelshub.github.io/sfall-documentation/arrays/
                    int idx = 0;
                    do
                    {
                        idx = line.IndexOf('[', idx);
                        if (idx == -1)
                            break;
                        int value = memoryArgs.ResolveAddress(line, out string literal);
                        line = line.Replace($"[{literal}]", "0x" + value.ToString("x"));
                    } while (idx != -1);
                    ssl.AddCustomCode(line);
                }
            }
        }

        public List<string> Run(List<ISSLPreProcessor> preProcessors)
        {
            List<string> result = new List<string>();

            if (runMode == RunMode.Memory)
            {
                new Fallout2().WriteMemoryPatches(memoryBytes);
            }
            else
            {
                result.AddRange(ssl.Get(runMode, preProcessors, this.parseEvents));
            }

            return result;
        }
    }
}
