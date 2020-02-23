using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace sfall_asm
{
    class Fallout2
    {
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

        private readonly Process fallout2 = null;

        public Fallout2()
        {
            var pids = Process.GetProcessesByName("Fallout2");
            if (pids.Length == 0)
                Error.Fatal("Unable to find a valid Fallout2.exe process", ErrorCodes.UnableToFindFallout2);
            fallout2 = pids[0];
        }

        public void WriteMemoryPatches(List<MemoryPatch> patches)
        {
            var ptr = OpenProcess(0x1F0FFF, IntPtr.Zero, new IntPtr(fallout2.Id));
            foreach (var patch in patches)
                WriteProcessMemory(ptr, (IntPtr)patch.offset, patch.data, patch.data.Length, out IntPtr _);
        }
    }

    class MemoryPatch
    {
        public byte[] data;
        public int offset;
    }
}