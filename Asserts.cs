using System;
using static sfall_asm.SSLCode;

namespace sfall_asm.Asserts
{
    class Sfall
    {
        public static void Assert(string name, int address, int offset, int bytes)
        {
            var fallout2 = new Fallout2();
            var baseaddress = fallout2.GetHookFuncOffset(address, offset);
            var memory = fallout2.ReadMemoryAt(baseaddress);
            var sfallCode = memory.ReadBytes(bytes);
            var code = new SSLCode();
            code.InlineProcedure = false;
            code.Name = "Assert" + name;
            var voodoo = new CodeGeneration.VoodooLib();
            code.Lines.Add(new Line(LineType.begin, 0, 0));
            code.Lines.Add(CodeGeneration.SSL.DeclareVariable("success").ToLine());
            code.Lines.Add(CodeGeneration.SSL.DeclareVariable("name").ToLine());
            code.Lines.Add(CodeGeneration.SSL.DeclareVariable("base").ToLine());
            code.AddCustomCode($"name := \"sfall::{name}\"");
            code.Lines.Add(voodoo.GetHookFuncOffset("base", address.ToHexString(), offset.ToHexString()).ToLine());
            for(var i=0;i<sfallCode.Length;i++)
            {
                code.Lines.Add(voodoo.AssertByte("success", $"name+\"+{i.ToHexString()}\"", "base+"+i.ToHexString(), sfallCode[i].ToHexString()).ToLine());
                code.AddCustomCode("if (success == false) then return false");
            }
            code.AddCustomCode("return true");
            code.Lines.Add(new Line(LineType.end, 0, 0));
            code.GetBody(Program.RunMode.Procedure).ForEach(x => Console.WriteLine(x));
        }
    }
}