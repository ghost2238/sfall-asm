using System;
using static sfall_asm.SSLCode;

namespace sfall_asm.Asserts
{
    class Sfall : CodeGeneration.Generator
    {
        public static void ParseAndRunAssertArgs(string args)
        {
            var assertArgs = args.Replace("--sfall-assert=", "");
            var spl = assertArgs.Split(',');
            if (spl.Length != 3)
            {
                Error.Fatal("--sfall-assert must use 3 args.", ErrorCodes.InvalidArgument);
            }
            var name = spl[0];
            int address = -1;
            try
            {
                address = Convert.ToInt32(spl[1], 16);
            }
            catch (Exception)
            {
                Error.Fatal("Unable to parse assert offset.", ErrorCodes.InvalidMemoryAddress);
            }
            if (!int.TryParse(spl[2], out int bytes))
            {
                Error.Fatal("Unable to parse assert bytes.", ErrorCodes.InvalidArgument);
            }

            new Sfall().Assert(name, address, 0, bytes);
        }

        public void Assert(string name, int address, int offset, int bytes)
        {
            var fallout2    = new Fallout2();
            var baseaddress = fallout2.GetHookFuncOffset(address, offset);
            var sfallCode   = fallout2.ReadMemoryAt(baseaddress).ReadBytes(bytes);

            code.InlineProcedure = false;
            code.Name = "Assert" + name;

            Begin();
            DeclareVar("name");
            DeclareVar("base");
            Add($"name := \"sfall::{name}\"");
            Add(voodoo.GetHookFuncOffset("base", address.ToHexString(), offset.ToHexString()).Code);
            for(var i=0;i<sfallCode.Length;i++)
            {
                var call = voodoo.AssertByte("==", $"name+\"+{(i + offset).ToHexString()}\"", "base+"+(i + offset).ToHexString(), sfallCode[i].ToHexString());
                Add($"if ({call.Code} == false) then return false");
            }
            Add("return true");
            End();
            code.GetBody(Program.RunMode.Procedure).ForEach(x => Console.WriteLine(x));
        }
    }
}