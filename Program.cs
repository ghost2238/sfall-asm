using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace sfall_asm
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(System.AppDomain.CurrentDomain.FriendlyName + " [asm_patch]");
                return;
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

            var output = new List<string>();
            var lastOffset = 0;
            foreach(var line in lines)
            {
                if (!line.Contains('|'))
                    continue;

                var spl = line.Split('|');
                if (spl.Length != 3)
                    continue;

                 
                var offset = Convert.ToInt32(spl[0].Trim(), 16);
                if (offset == 0)
                    offset = lastOffset;
                
                var bytes = spl[1].Replace(" ", "");
                for (var i= 0; i < bytes.Length; i+=2)
                {
                    var writeByte = $"write_byte(0x{offset.ToString("x")}, 0x{bytes[i]}{bytes[i + 1]})";
                    if (i == 0)
                        writeByte += "; /* " + spl[2].Trim() + " */ ";
                    else
                        writeByte += ";";
                    writeByte += " \\";
                    output.Add(writeByte);
                    offset++;
                    lastOffset = offset;
                }
            }

            foreach (var l in output)
                Console.WriteLine(l);

            // Console.ReadKey();
            //var lines = ;
            //foreach(var line in lines)
        }
    }
}
