using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static sfall_asm.Program;

namespace sfall_asm
{
    public class SSLCode
    {
        // NOTE: enum names are used as-is in generated code (in most cases)
        public enum LineType
        {
            begin,
            end,
            noop,
            write_byte,
            write_short,
            write_int,
            comment,
            code // custom code
        };

        public class Line
        {
            public LineType Type;
            public bool TypeIsWrite => Type == LineType.write_byte || Type == LineType.write_short || Type == LineType.write_int;

            public int Address;
            public int Value;
            public string Code;
            public string Comment;

            public bool RFall;
            public bool HRP;

            public string HexFormat;

            public string FunctionString => (RFall ? "r_" : "") + Enum.GetName(typeof(LineType), Type);
            public string AddressString => (HRP ? "r_hrp_offset(" : "") + "0x" + Address.ToString(HexFormat) + (HRP ? ")" : "");
            public string ValueString
            {
                get
                {
                    string result = "";

                    if (Type == LineType.write_byte)
                        result = ((byte)Value).ToString($"{HexFormat}2");
                    else if (Type == LineType.write_short)
                        result = ((short)Value).ToString($"{HexFormat}4");
                    else if (Type == LineType.write_int)
                        result = Value.ToString($"{HexFormat}8");

                    if (result.Length > 0)
                        result = $"0x{result}";

                    return result;
                }
            }
            

            public Line(string code)
            {
                Type = LineType.code;
                Code = code;
                Address = 0;
                Value = 0;
                Comment = "";
                RFall = HRP = false;
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

        public readonly string NamePrefix = "";
        public string Name = "";
        public bool Lower = true;
        public bool MacroGuard = true;
        public bool Pack = true;
        public bool RFall = false;

        protected List<string> Info = new List<string>();
        public List<Line> Lines = new List<Line>();
        protected Line LastLine;
        protected Line LastSemicolonLine;

        public SSLCode(string namePrefix = "")
        {
            NamePrefix = namePrefix;
        }

        public SSLCode(SSLCode other)
        {
            NamePrefix = other.NamePrefix;

            /*
            Name = other.Name;
            */

            Lower = other.Lower;
            MacroGuard = other.MacroGuard;
            Pack = other.Pack;
            RFall = other.RFall;

            /*
            Info = other.Info;
            Lines = other.Lines;
            LastLine = other.LastLine;
            LastSemicolonLine = other.LastSemicolonLine;
            */
        }

        public void AddInfo(string info)
        {
            Info.Add(info);
        }

        protected void AddLine(Line line, bool first = false)
        {
            if (first)
                Lines.Insert(0, line);
            else
            {
                Lines.Add(line);
                LastLine = Lines.Last();
            }
        }

        public void AddCustomCode(string code)
        {
            code = Regex.Replace(code, @"[\t ]*;[\t ]*$", "");

            AddLine(new Line(code));
            LastSemicolonLine = LastLine;
        }

        public void AddWrite(int size, int address, int value, string comment = "")
        {
            LineType type;

            if (size == 4)
                type = LineType.write_int;
            else if (size == 2)
                type = LineType.write_short;
            else if (size == 1)
                type = LineType.write_byte;
            else
                throw new ArgumentOutOfRangeException(nameof(size));

            AddLine(new Line(type, address, value, comment));
            LastSemicolonLine = LastLine;

            // vanilla sfall cannot write outside Fallout2.exe memory currently,
            // after preparing all lines, code is tweaked to use less restricted rfall implementation
            // see sfall/Modules/Scripting/Handlers/Memory.cpp -- START_VALID_ADDR, END_VALID_ADDR
            if (address < 0x410000 || address > 0x6B403F)
                LastLine.RFall = true;

            // f2_res.dll base address might change in some conditions
            // make sure macro/procedure is writing at correct position by tweaking address same way as sfall does
            // see sfall/main.cpp -- HRPAddress()
            if (address >= 0x10000000 && address <= 0x10077000)
                LastLine.RFall = LastLine.HRP = true;

            // add r_ prefix to ALL lines if at least one write uses rfall function or --rfall is used
            // in first case it's technically not needed to use r_write_* if other address(es) are inside sfall limits,
            // but mixing limited and non-limited writing can make macro/procedure useless and/or dangerous
            if (!LastLine.RFall && this.RFall)
                LastLine.RFall = true;
            else if (LastLine.RFall && !this.RFall)
            {
                foreach (Line line in Lines)
                {
                    line.RFall = true;
                }

                this.RFall = true;
            }
        }

        public void AddComment(string comment)
        {
            AddLine(new Line(LineType.comment, 0, 0, comment));
        }

        protected void PreProcessNOP()
        {
            bool isNOP8(int idx) => Lines[idx].Type == LineType.write_byte && Lines[idx].Value == 0x90 && Lines[idx].Comment == "nop";
            bool isNOP16(int idx) => Lines[idx].Type == LineType.write_short && Lines[idx].Value == 0x9090 && Lines[idx].Comment == "nop";
            bool isNOP32(int idx) => Lines[idx].Type == LineType.write_int && (uint)Lines[idx].Value == 0x90909090 && Lines[idx].Comment == "nop";

            for (int l = 0, len = Lines.Count; l < len; l++)
            {
                // lower amount of lines generated for duplicated NOP instructions
                if (Pack)
                {
                    // byte, byte, byte, byte -> int
                    if (l + 4 <= len && isNOP8(l) && isNOP8(l + 1) && isNOP8(l + 2) && isNOP8(l + 3))
                    {
                        Lines[l].Type = LineType.write_int;
                        Lines[l].Value = unchecked((int)0x90909090);

                        Lines.RemoveAt(l + 1);
                        Lines.RemoveAt(l + 1);
                        Lines.RemoveAt(l + 1);

                        len -= 3;
                    }
                    // short, short -> int
                    else if (l + 2 <= len && isNOP16(l) && isNOP16(l + 1))
                    {
                        Lines[l].Type = LineType.write_int;
                        Lines[l].Value = unchecked((int)0x90909090);

                        Lines.RemoveAt(l + 1);

                        len--;
                    }
                    // byte, byte -> short
                    else if (l + 2 <= len && isNOP8(l) && isNOP8(l + 1))
                    {
                        Lines[l].Type = LineType.write_short;
                        Lines[l].Value = 0x9090;

                        Lines.RemoveAt(l + 1);

                        len--;
                    }
                }

                if (isNOP32(l))
                    Lines[l].Comment = "nop; nop; nop; nop";
                else if (isNOP16(l))
                    Lines[l].Comment = "nop; nop";
            }
        }

        protected void PreProcessMacro()
        {
            if (MacroGuard)
            {
                int bodyCount = 0;
                foreach (Line line in Lines)
                {
                    switch (line.Type)
                    {
                        case LineType.write_byte:
                        case LineType.write_short:
                        case LineType.write_int:
                        case LineType.code:
                            bodyCount++;
                            break;
                    }

                    if (bodyCount >= 2)
                        break;
                }

                if (bodyCount >= 2)
                {
                    AddLine(new Line(LineType.begin, 0, 0), true);
                    AddLine(new Line(LineType.end, 0, 0));
                    AddLine(new Line(LineType.noop, 0, 0));
                }
            }
        }

        protected void PreProcessProcedure()
        {
            AddLine(new Line(LineType.begin, 0, 0), true);
            AddLine(new Line(LineType.end, 0, 0));
        }

        public void PreProcess(RunMode mode)
        {
            PreProcessNOP();

            if (mode == RunMode.Macro)
                PreProcessMacro();
            else if (mode == RunMode.Procedure)
                PreProcessProcedure();
        }

        public string GetName()
        {
            string result = "";

            if (NamePrefix.Length > 0)
                result += NamePrefix + "_";

            if (Name.Length > 0)
                result += Name;

            return result;
        }

        public List<string> GetInfo()
        {
            List<string> result = new List<string>();

            foreach (string info in Info)
            {
                result.Add($"// {info}");
            }

            bool hrp = false, rfall = false;
            foreach (var line in Lines)
            {
                if (!hrp && line.HRP)
                {
                    hrp = true;
                    result.Add("// hrp required");
                }

                if (!rfall && line.RFall)
                {
                    rfall = true;
                    result.Add("// rfall required");
                }

                if (rfall && hrp)
                    break;
            }

            return result;
        }

        // should be called only after preprocessing
        public SortedDictionary<int,int> GetWriteGroups()
        {
            SortedDictionary<int,int> groups = new SortedDictionary<int,int>();
            int lastGroup = -1;

            foreach(Line line in Lines)
            {
                if(!line.TypeIsWrite)
                    continue;

                int size = 0;
                switch(line.Type)
                {
                    case LineType.write_int:
                        size = 4;
                        break;
                    case LineType.write_short:
                        size = 2;
                        break;
                    case LineType.write_byte:
                        size = 1;
                        break;
                }

                if(lastGroup < 0 || line.Address != groups[lastGroup])
                {
                    groups[line.Address] = line.Address + size;
                    lastGroup = line.Address;
                }
                else
                    groups[lastGroup] += size;
            }

            return groups;
        }

        public List<string> GetBody(RunMode mode)
        {
            if (mode != RunMode.Macro && mode != RunMode.Procedure)
                throw new InvalidOperationException("You're kidding, right?");

            List<string> result = new List<string>();
            string resultmp;

            int bodyCount = 0;

            // collect maximum length of each subelement
            int maxFunctionLength = 0, maxAddressLength = 0, maxValueLength = 0, maxCommentLength = 0;
            int maxRawWriteMacroLength, maxRawWriteProcedureLength = 0, maxRawCommentLength = 0;
            int maxRawCodeLength = 0, maxRawLength = 0;

            foreach (Line line in Lines)
            {
                if (line.TypeIsWrite)
                {
                    bodyCount++;
                    line.HexFormat = Lower ? "x" : "X";

                    maxFunctionLength = Math.Max(maxFunctionLength, line.FunctionString.Length);
                    maxAddressLength = Math.Max(maxAddressLength, line.AddressString.Length);
                    maxValueLength = Math.Max(maxValueLength, line.ValueString.Length);
                    maxCommentLength = Math.Max(maxCommentLength, line.Comment.Length);
                }
                else if (line.Type == LineType.code)
                {
                    maxRawCodeLength = Math.Max(maxRawCodeLength, line.Code.Length);
                }
                else if (line.Type == LineType.comment)
                {
                    maxRawCommentLength = Math.Max(maxRawCommentLength, line.Comment.Length);
                }
            }
            //                       write               (   0x1207             ,   _   0x1337           )   ;   _   /   *   _   text               _   *   /
            maxRawWriteMacroLength = maxFunctionLength + 1 + maxAddressLength + 1 + 1 + maxValueLength + 1 + 1 + 1 + 1 + 1 + 1 + maxCommentLength + 1 + 1 + 1;
            //                           write               (   0x1207             ,   _   0x1337           )   ;   _   /   /   _   text
            maxRawWriteProcedureLength = maxFunctionLength + 1 + maxAddressLength + 1 + 1 + maxValueLength + 1 + 1 + 1 + 1 + 1 + 1 + maxCommentLength;
            //                    /   *   _   text                 _   *   /
            maxRawCommentLength = 1 + 1 + 1 + maxRawCommentLength + 1 + 1 + 1;

            if (mode == RunMode.Macro)
            {
                maxRawLength = Math.Max(maxRawWriteMacroLength, maxRawCommentLength);
                maxRawLength = Math.Max(maxRawLength, maxRawCodeLength + 1);
            }
            else if (mode == RunMode.Procedure)
            {
                maxRawLength = Math.Max(maxRawWriteProcedureLength, maxRawCommentLength);
                maxRawLength = Math.Max(maxRawLength, maxRawCodeLength + 1);
            }

            string prefix = "";
            if (mode == RunMode.Macro)
            {
                prefix = new string(' ', 8 + (NamePrefix.Length > 0 ? NamePrefix.Length + 1 : 0));

                result.Add($"#define {GetName()} \\");
            }
            else if (mode == RunMode.Procedure)
            {
                prefix = new string(' ', 3); // default SFallEditor setting... yeah. i know.

                result.Add($"inline procedure {GetName()}");
            }

            foreach (var line in Lines)
            {
                resultmp = prefix;

                string semicolon()
                {
                    string semicolon_result = ";";

                    if (mode == RunMode.Macro)
                    {
                        if (!MacroGuard && line == LastSemicolonLine)
                            semicolon_result = " ";
                        else if (bodyCount == 1)
                            semicolon_result = " ";
                    }

                    return semicolon_result;
                }

                string comment(string value) => (mode == RunMode.Macro ? $"/* {value} */" : $"// {value}");

                if (line.Type == LineType.begin || line.Type == LineType.end || line.Type == LineType.noop)
                {
                    bool rfall = line.RFall;
                    line.RFall = false;

                    // align begin/end to right when generating procedure
                    if (mode == RunMode.Procedure)
                        resultmp = " ";

                    resultmp = resultmp.Remove(resultmp.Length - 1);
                    resultmp += line.FunctionString.PadRight(maxRawLength);

                    if (line.Type == LineType.noop && line != LastLine)
                        resultmp += ";";

                    line.RFall = rfall;
                }
                else if (line.TypeIsWrite)
                {
                    resultmp += line.FunctionString.PadRight(maxFunctionLength);
                    resultmp += $"({line.AddressString}, ".PadRight(maxAddressLength + 3);
                    resultmp += $"{line.ValueString}){semicolon()} ".PadRight(maxValueLength + 3);

                    if (line.Comment.Length > 0)
                        resultmp += comment(line.Comment);
                }
                else if (line.Type == LineType.code)
                {
                    resultmp += $"{line.Code}{semicolon()}".PadRight(maxRawCodeLength + 3);
                    if (line.Comment.Length > 0)
                        resultmp += comment(line.Comment);
                }
                else if (line.Type == LineType.comment)
                    resultmp += comment(line.Comment);

                if (mode == RunMode.Macro)
                {
                    // make sure all line types are same length before adding suffix
                    resultmp = resultmp.PadRight(prefix.Length + maxRawLength);

                    if (line != LastLine)
                        resultmp += " \\";
                }

                result.Add(resultmp.TrimEnd());
            }

            return result;
        }

        public List<string> Get(RunMode mode, List<ISSLPreProcessor> preProcessors, List<ParseEventInfo> parseEvents)
        {
            PreProcess(mode);
            preProcessors.ForEach(x => x.Process(this, parseEvents));

            List<string> result = new List<string>();

            result.AddRange(GetInfo());
            result.AddRange(GetBody(mode));

            return result;
        }
    }
}
