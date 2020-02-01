using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace sfall_asm
{
    class Program
    {

        const int PROCESS_VM_OPERATION = 0x08;
        const int PROCESS_VM_READ = 0x10;
        const int PROCESS_VM_WRITE = 0x20;
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

        class MemoryPatch
        {
            public byte[] data;
            public int offset;
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(System.AppDomain.CurrentDomain.FriendlyName + " [asm_patch]");
                return;
            }

            bool memory = false;
            if(args.Length>1)
            {
                foreach (var a in args)
                    if (a == "--memory")
                        memory = true;
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
            catch (Exception ex) { 
                Console.WriteLine("Unable to open file: " + ex.Message);
                Environment.Exit(1);
            };


            Process fallout2=null;
            if(memory)
            {
                var pids = Process.GetProcessesByName("Fallout2");
                if (pids.Length == 0)
                {
                    Console.WriteLine("Unable to find fallout2.exe");
                    return;
                }
                fallout2 = pids[0];
            }

            var memorybytes = new List<MemoryPatch>();
            var output = new List<string>();
            var lastOffset = 0;
            foreach (var line in lines)
            {
                if (line.Length >= 2 && line[0] == '/' && line[1] == '/')
                    continue;

                if (!line.Contains('|'))
                    continue;

                var spl = line.Split('|');
                if (spl.Length != 3)
                    continue;

                var offset = Convert.ToInt32(spl[0].Trim(), 16);
                if (offset == 0)
                    offset = lastOffset;

                var bytes = spl[1].Replace(" ", "");
                for (var i = 0; i < bytes.Length; i += 2)
                {
                    if (memory)
                    {
                        var by = Convert.ToByte($"{bytes[i]}{bytes[i + 1]}", 16);
                        memorybytes.Add(new MemoryPatch()
                        {
                            data = new byte[] { by },
                            offset = offset
                        });
                    }
                    else
                    {
                        var writeByte = $"write_byte(0x{offset.ToString("x")}, 0x{bytes[i]}{bytes[i + 1]})";
                        if (i == 0)
                            writeByte += "; /* " + spl[2].Trim() + " */ ";
                        else
                            writeByte += ";";
                        writeByte += " \\";
                        output.Add(writeByte);
                    }
                    offset++;
                    lastOffset = offset;
                }
            }

            if (memory)
            {
                var ptr = OpenProcess(0x1F0FFF, IntPtr.Zero, new IntPtr(fallout2.Id));
                //var hProc = new IntPtr(fallout2.Id);
                foreach (var patch in memorybytes)
                {
                   // VirtualProtectEx(ptr, (IntPtr)patch.offset, (UIntPtr)1, 0x40, out uint _);
                    WriteProcessMemory(ptr, (IntPtr)patch.offset, patch.data, patch.data.Length, out IntPtr _);
                }
            }
            else
            {
                foreach (var l in output)
                    Console.WriteLine(l);
            }


            // Console.ReadKey();
            //var lines = ;
            //foreach(var line in lines)
        }
    }
}
