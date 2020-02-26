using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace sfall_asm
{
    public interface ISSLPreProcessor
    {
        void Process(SSLCode code, List<ParseEventInfo> ParseEvents);
    }

    public interface IASMParser
    {
        bool Process(ByteString byteCode, int offset, Patch patch, List<ParseEventInfo> ParseEvents);
    }

    // Rewrite Call/Jmp instructions (only E8/E9 now) to use VOODOO_MakeJump and VOODOO_MakeCall.
    public class JumpASMRewriter : IASMParser
    {
        bool IASMParser.Process(ByteString bytes, int offset, Patch patch, List<ParseEventInfo> ParseEvents)
        {
            // Duplicates some code from Patch.cs
            var isJump = (bytes.PeekByte() == 0xE9);
            var isCall = (bytes.PeekByte() == 0xE8);
            if (isJump || isCall)
            {
                if (bytes.PeekChar(2) == '[')
                {
                    int destination = patch.memoryArgs.ResolveAddress(bytes.RawString, out string resolvedLiteral);

                    var jumpType = isJump ? "Jump" : "Call";

                    patch.ssl.AddCustomCode($"call VOODOO_Make{jumpType}(0x{offset.ToString("x")}, 0x{destination.ToString("x")})");
                    patch.lastOffset = patch.currentOffset + 5;
                    return true;
                }
            }

            return false;
        }
    }

    // Rewrites write_ to use malloc'd memory.
    public class MallocPreProcessor : ISSLPreProcessor
    {
        private int bytes = 0;
        private bool debugCode; // generate debug code.
        private List<ParseEventInfo> ParseEvents;
        private Regex parseMalloc;
        private Regex parseCallArgs;
        public MallocPreProcessor(bool debugCode)
        {
            this.parseCallArgs = new Regex("VOODOO_Make(.+?)\\((.+?),(.+?)\\)");
            this.parseMalloc = new Regex("malloc\\((.+?)\\)");
            this.debugCode = debugCode;
        }

        public bool AddressDefinedAsVariable(int address)
        {
            foreach (var ev in ParseEvents)
            {
                if (ev.@event == ParseEvent.ResolvedAddressMemoryArg)
                {
                    var info = ev.GetMemoryArgInfo();
                    if (info.address == address)
                        return true;
                }
            }
            return false;
        }

        public MemoryArgInfo GetAddressVariable(int address)
        {
            foreach(var ev in ParseEvents)
            {
                if (ev.@event == ParseEvent.ResolvedAddressMemoryArg) 
                { var info = ev.GetMemoryArgInfo(); 
                  if(info.address == address)
                    return info;
                }
            }
            return null;
        }

        public void Process(SSLCode code, List<ParseEventInfo> ParseEvents)
        {
            this.ParseEvents = ParseEvents;
            bytes = 0;
            var currentName = "";
            var startIdx = -1;
            var startAddress = -1;
            List<string> mallocVars = new List<string>();
            bool processWrite = false;
            for (int i = 0; i < code.Lines.Count; i++)
            {
                var line = code.Lines[i];
                var variable = GetAddressVariable(line.Address);
                if (variable != null && mallocVars.Contains(variable.name))
                {
                    currentName = variable.name;
                    startIdx = i;
                    startAddress = line.Address;
                    processWrite = true;
                }
                
                if (line.Type == SSLCode.LineType.comment && this.parseMalloc.IsMatch(line.Comment))
                {
                    var m = this.parseMalloc.Match(line.Comment);
                    if(m.Success)
                        mallocVars.Add(m.Groups[1].Value);
                }

                if (currentName == "")
                    continue;

                // Bad hack
                if (line.Type == SSLCode.LineType.comment && line.Comment == ("END"))
                    break;

                if (line.Type == SSLCode.LineType.code)
                {
                    if (this.parseCallArgs.IsMatch(line.Code))
                    {
                        var m = this.parseCallArgs.Match(line.Code);
                        var addr = Convert.ToInt32(m.Groups[2].Value, 16);
                        var offset = addr - startAddress;
                        line.Code = $"call VOODOO_Make{m.Groups[1]}(addr+{offset},{m.Groups[3]})";
                        bytes += 5;
                    }
                }

                if(line.TypeIsWrite)
                {
                    var checkVar = GetAddressVariable(line.Address);
                    if (checkVar != null)
                    {
                        // Found another [variable], we are done.
                        if(processWrite && checkVar.name != currentName)
                            break;
                    }
                    if (!processWrite)
                        continue;

                    var offset = code.Lines[i].Address - startAddress;
                    code.Lines[i] = ConvertWrite(code, line, offset);
                }
            }
            if (startIdx != -1)
            {
                code.Lines.Insert(startIdx, new SSLCode.Line("addr := VOODOO_nmalloc(" + bytes + ")"));
                if (debugCode)
                    code.Lines.Insert(startIdx+1, new SSLCode.Line($"debug(\"Allocated {bytes} bytes @ \"+ addr)"));
            }
        }

        public SSLCode.Line ConvertWrite(SSLCode code, SSLCode.Line line, int offset)
        {
            string newCode = "";
            string func="";
            if (line.Type == SSLCode.LineType.write_byte)
            {
                bytes += 1;
                func = "call VOODOO_SafeWrite8 ";
            }
            if (line.Type == SSLCode.LineType.write_short)
            {
                bytes += 2;
                func = "call VOODOO_SafeWrite16";
            }
            if (line.Type == SSLCode.LineType.write_int)
            {
                bytes += 4;
                func = "call VOODOO_SafeWrite32";
            }
            line.HexFormat = code.Lower ? "x" : "X";
            newCode = $"{func}(addr+{offset}, {line.ValueString})";

            var newLine = new SSLCode.Line(newCode);
            newLine.Comment = line.Comment;
            return newLine;
        }
    }
}