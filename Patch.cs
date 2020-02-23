using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static sfall_asm.Program;

namespace sfall_asm
{
    // Bytestring which can also contain memory literals.
    public class ByteString
    {
        private string str;
        public int offset;
        public ByteString(string str)
        {
            this.str = str.Replace(" ", "");
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
            // This might not be the best solution for https://github.com/ghost2238/sfall-asm/issues/4
            if (PeekChar(0) == ':')
                offset++;

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
    }

    class Patch
    {
        private List<string> lines;
        private RunMode runMode;
        private SSLCode ssl;
        private MemoryArgs memoryArgs;
        private List<MemoryPatch> memoryBytes;

        public Patch(List<string> lines, RunMode runMode, SSLCode ssl, MemoryArgs memoryArgs)
        {
            this.lines = lines;
            this.runMode = runMode;
            this.ssl = ssl;
            this.memoryArgs = memoryArgs;
            this.Parse();
        }

        public void Parse()
        {
            this.memoryBytes = new List<MemoryPatch>();
            var lastOffset = 0;

            var reMeta = new Regex(@"^//![\t ]+([A-Za-z0-9]+)[\t ]+(.+)$");
            var reInfo = new Regex(@"^///[\t ]+(.+)$");
            var reAddr = new Regex(@"^[\t ]*(|\$[\+\-][A-Fa-f0-9]+[\t ]+|\$[\t ]+[=]+>[\t ]+)([A-Fa-f0-9]+)");
            bool bodyStart = false;

            Match match;
            ParseMode mode = ParseMode.ASM;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                // Find additional patch configuration or mode switches
                match = reMeta.Match(line);
                if (match.Success)
                {
                    string var = match.Groups[1].Value.Trim().ToUpper();
                    string val = match.Groups[2].Value.Trim();

                    // Changes "///" behavior to include comment inside macro/procedure body
                    if (var == "BODY")
                        bodyStart = true;
                    // Sets macro/procedure name
                    else if (var == "NAME")
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
                            spl[0] = memoryArgs.ResolveAddress(spl[0], out _).ToString("x");
                    }

                    if (spl[0].Length == 0)
                        continue;

                    // changes "///" behavior to include comment inside macro/procedure body
                    bodyStart = true;

                    var offset = Convert.ToInt32(spl[0].Trim(), 16);
                    if (offset == 0)
                        offset = lastOffset;

                    var bytes = new ByteString(spl[1]);
                    bool opLine = true;
                    while (!bytes.EOF)
                    {
                        // Instructions which supports using absolute->rel addressing or memory variable.
                        // https://github.com/ghost2238/sfall-asm/issues/3
                        // This only works for 32-bit, if we are in 16-bit mode it won't work.
                        // But since we don't care about that at the moment it should be fine.
                        if ((bytes.PeekByte() == 0xE9) || (bytes.PeekByte() == 0xE8))
                        {
                            if (bytes.PeekChar(2) == '[')
                                bytes.ResolveMemoryArg(memoryArgs, offset);
                        }

                        if (runMode == RunMode.Memory)
                        {
                            memoryBytes.Add(new MemoryPatch()
                            {
                                data = new byte[] { bytes.ReadByte() },
                                offset = offset
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

                            ssl.AddWrite(writeSize, offset, bytes.AsInt(writeSize), opLine ? spl[2].Trim() : "");
                            // this is to decide if comment should be written or not.
                            opLine = false;
                            offset += writeSize - 1;
                        }

                        offset++;
                        lastOffset = offset;
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

        public void Run()
        {
            if (runMode == RunMode.Memory)
            {
                new Fallout2().WriteMemoryPatches(memoryBytes);
            }
            else
            {
                foreach (var line in ssl.Get(runMode))
                {
                    Console.WriteLine(line);
                }
            }
        }
    }
}