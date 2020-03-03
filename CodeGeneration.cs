using static sfall_asm.SSLCode;

namespace sfall_asm.CodeGeneration
{
    public enum SSLTokenType
    {
        VariableDeclaration,
        FunctionCall,
        MacroUse,
        Comment
    }

    public class SSLToken
    {
        public SSLTokenType Type;
        public string Code;
        public SSLCode.Line ToLine() => new SSLCode.Line(Code);
    }

    public class MallocVar
    {
        public int Id;
        public string Name;
        public MallocVar(string name)
        {
            this.Name = "VOODOO_ID_"+name;
            this.Id = PatchEngine.Get().DeclareMallocVar(name);
        }
    }

    public static class SSL
    {
        private static string Arguments(string[] args) => "(" + string.Join(", ", args) + ")";

        public static SSLToken UseMacro(string name, params string[] args)
        {
            string Code = "";
            Code += name;
            Code += Arguments(args);
            return new SSLToken()
            {
                Type = SSLTokenType.MacroUse,
                Code = Code
            };
        }

        public static SSLToken DeclareVariable(string name)
        {
            return new SSLToken()
            {
                Type = SSLTokenType.VariableDeclaration,
                Code = $"variable {name}"
            };
        }

        public static SSLToken OP(string name, string retVariable = null, params string[] args)
        {
            string Code = "";
            if (retVariable != null)
                Code = $"{retVariable} := ";
            Code += name;
            Code += Arguments(args);
            return new SSLToken()
            {
                Type = SSLTokenType.FunctionCall,
                Code = Code
            };
        }

        public static SSLToken Function(string name, string retVariable =null, params string[] args)
        {
            string Code = "";
            if (retVariable != null && retVariable != "==") // == means it's used in an expression. 
                Code = $"{retVariable} := ";
            Code += name;
            Code += Arguments(args);
            if (retVariable == null)
                Code = "call " + Code;
            return new SSLToken()
            {
                Type = SSLTokenType.FunctionCall,
                Code = Code
            };
        }
    }

    // Various macros and procedures found in https://github.com/rotators/Fo1in2/tree/master/Fallout2/Fallout1in2
    class Rotators
    {
        public static SSLToken Debug(string str) => SSL.UseMacro("debug", str);
        
    }

    // sfall - https://fakelshub.github.io/sfall-documentation/
    // vanilla - https://falloutmods.fandom.com/wiki/Fallout_1_and_Fallout_2_scripting_-_commands,_reference,_tutorials
    class ScriptFunctions
    {
        
    }

    // Standard library used for various low-level interaction.
    // https://github.com/rotators/Fo1in2/blob/master/Fallout2/Fallout1in2/Mapper/source/scripts/headers/voodoo_lib.h
    class VoodooLib
    {
        public SSLToken AssertByte(string retVar, string addressName, string address, string expected)
        {
            return SSL.Function("VOODOO_AssertByte", retVar, addressName, address, expected);
        }

        public SSLToken BlockCall(int address, int length)
        {
            length = MathUtils.Clamp(length, 5, 15);
            return BlockCall(address.ToString(), length.ToString());
        }
        public SSLToken BlockCall(string address, string length) => SSL.Function(null, address, length);

        public SSLToken nmalloc(string retVal, int bytes)
            => SSL.Function("VOODOO_nmalloc", retVal, bytes.ToString());
        public SSLToken memset(string address, int val, int size) => SSL.Function("VOODOO_memset", null, address, "0x" + val.ToString("x"), size.ToString());

        public SSLToken MakeJump(string address, string func) => SSL.Function("VOODOO_MakeJump", null, address, func);
        public SSLToken MakeJump(string address, int func)    => SSL.Function("VOODOO_MakeJump", null, address, "0x" + func.ToString("x"));
        public SSLToken MakeCall(string address, string func) => SSL.Function("VOODOO_MakeCall", null, address, func);
        public SSLToken MakeCall(string address, int func)    => SSL.Function("VOODOO_MakeCall", null, address, "0x"+func.ToString("x"));

        public SSLToken SetLookupData(MallocVar var, string value, int size) => SSL.Function("VOODOO_SetLookupData", null, var.Name, value, size.ToString());
        public SSLToken GetAddressOf(string retVar, string var) => SSL.Function("VOODOO_GetAddressOf", retVar, var);

        // Underlying methods might change.
        public SSLToken Write8(string address, string value)  => SSL.OP("write_byte", null, address, value);
        public SSLToken Write16(string address, string value) => SSL.OP("write_short", null, address, value);
        public SSLToken Write32(string address, string value) => SSL.OP("write_int", null, address, value);

        public SSLToken GetHookFuncOffset(string retVar, string address, string offset) => SSL.Function("VOODOO_GetHookFuncOffset", retVar, address, offset);
    }

    class Generator
    {
        protected VoodooLib voodoo = new VoodooLib();
        protected SSLCode code = new SSLCode();

        protected void Add(string code) => this.code.AddCustomCode(code);
        protected void Begin() => this.code.Lines.Add(new Line(LineType.begin, 0, 0));
        protected void End() => this.code.Lines.Add(new Line(LineType.end, 0, 0));
        protected void DeclareVar(string name) => code.Lines.Add(SSL.DeclareVariable(name).ToLine());
    }
}
