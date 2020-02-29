namespace sfall_asm.CodeGeneration
{
    public enum SSLTokenType
    {
        VariableDeclaration,
        FunctionCall,
        MacroUse
    }

    public class SSLToken
    {
        public SSLTokenType Type;
        public string Code;
        public SSLCode.Line ToLine() => new SSLCode.Line(Code);
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

        public static SSLToken Function(string name, string retVariable =null, params string[] args)
        {
            string Code = "";
            if (retVariable != null)
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
        public SSLToken BlockCall(int address, int length)
        {
            length = MathUtils.Clamp(length, 5, 15);
            return BlockCall(address.ToString(), length.ToString());
        }
        public SSLToken BlockCall(string address, string length) => SSL.Function(null, address, length);

        public SSLToken nmalloc(string retVal, int bytes)
            => SSL.Function("VOODOO_nmalloc", retVal, bytes.ToString());

        public SSLToken MakeJump(string address, string func) => SSL.Function("VOODOO_MakeJump", null, address, func);
        public SSLToken MakeJump(string address, int func)    => SSL.Function("VOODOO_MakeJump", null, address, func.ToString());
        public SSLToken MakeCall(string address, string func) => SSL.Function("VOODOO_MakeCall", null, address, func);
        public SSLToken MakeCall(string address, int func)    => SSL.Function("VOODOO_MakeCall", null, address, func.ToString());


        // Underlying methods might change.
        public SSLToken Write8(string address, string value) => SSL.Function("VOODOO_SafeWrite8", null, address, value);
        public SSLToken Write16(string address, string value) => SSL.Function("VOODOO_SafeWrite16", null, address, value);
        public SSLToken Write32(string address, string value) => SSL.Function("VOODOO_SafeWrite32", null, address, value);
    }

    class Generators
    {
        public static VoodooLib VoodooLib => new VoodooLib();
    }
}
