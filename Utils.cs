using System;
using System.Linq;

namespace sfall_asm
{
    public enum ErrorCodes
    {
        EmptyMemoryAddress = 1,
        UnableToResolveMemoryAddress,
        InvalidMemoryAddress,
        InvalidArgument,
        ParseErrorMemoryAddress,
        FileDoesntExist,
        UnableToOpenFile,
        InvalidUpdateFile,
        MallocRequired,
        UnableToFindFallout2,
        UnhandledException
    }

    public static class MathUtils
    {
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }
    }

    public static class Error
    {
        public static bool Strict;
        public static Func<string> GetErrorContext;

        private static string GetContext()
        {
            if (GetErrorContext == null)
                return "";
            return GetErrorContext() + ": ";
        }

        // Time for some guru meditation...
        public static void Fatal(string error, ErrorCodes code)
        {
            if (System.Diagnostics.Debugger.IsAttached)
                throw new Exception($"{code.ToString()}: {error}");
            Console.Error.WriteLine($"{GetContext()}{error}");
            Environment.Exit((int)code);
        }
    }

    public static class ASM
    {
        // Swap from big-endian to little-endian (or back, if already swapped).
        public static int SwapEndian(int num)
        {
            byte[] end = BitConverter.GetBytes(num).Reverse().ToArray();
            return BitConverter.ToInt32(end, 0);
        }

        public static short SwapEndian(short num)
        {
            byte[] end = BitConverter.GetBytes(num).Reverse().ToArray();
            return BitConverter.ToInt16(end, 0);
        }

        // Calculate the jump distance for relative jump/call.
        public static int CalculateRelJump32(int from, int to) => SwapEndian(to - from - 5);
    }

    public static class Extensions
    {
        public static string ToHexString(this int i) => "0x" + i.ToString("x");
        public static string ToHexString(this short i) => "0x" + i.ToString("x");
        public static string ToHexString(this byte i) => "0x" + i.ToString("x");
    }

}