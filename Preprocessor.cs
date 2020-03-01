using sfall_asm.CodeGeneration;
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
        private VoodooLib voodoo = new VoodooLib();

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
                    var from = offset.ToHexString();
                    var to = destination.ToHexString();
                    var code = (isJump ?
                            voodoo.MakeJump(from, to)
                          : voodoo.MakeCall(from, to)).Code;

                    patch.ssl.AddCustomCode(code);
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
        private VoodooLib voodoo = new CodeGeneration.VoodooLib();

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
                        var isJump = m.Groups[1].Value == "Jump";
                        var from = $"$addr+{offset}";
                        var to = m.Groups[3].Value;

                        line.Code = (isJump ?
                            voodoo.MakeJump(from, to)
                          : voodoo.MakeCall(from, to)).Code;
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
                code.Lines.Insert(startIdx, voodoo.nmalloc("$addr", bytes).ToLine());
                if (debugCode)
                    code.Lines.Insert(startIdx+1, CodeGeneration.Rotators.Debug($"\"Allocated {bytes} bytes @ \"+ $addr").ToLine());
            }
        }

        public SSLCode.Line ConvertWrite(SSLCode code, SSLCode.Line line, int offset)
        {
            line.HexFormat = code.Lower ? "x" : "X";
            string address = $"$addr+{offset}";
            string value = line.ValueString;

            SSLCode.Line newLine=null;

            if (line.Type == SSLCode.LineType.write_byte)
            {
                bytes += 1;
                newLine = voodoo.Write8(address, value).ToLine();
            }
            else if (line.Type == SSLCode.LineType.write_short)
            {
                bytes += 2;
                newLine = voodoo.Write16(address, value).ToLine();
            }
            else if (line.Type == SSLCode.LineType.write_int)
            {
                bytes += 4;
                newLine = voodoo.Write32(address, value).ToLine();
            }
            newLine.Comment = line.Comment;
            return newLine;
        }
    }
}