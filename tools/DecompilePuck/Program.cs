using System;
using System.Linq;
using Mono.Cecil;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            var dlls = new[]
            {
                "..\\..\\libs\\Puck.dll",
                "..\\..\\libs\\Assembly-CSharp-firstpass.dll"
            };

            var outputPath = "output_all.txt";
            using var writer = new System.IO.StreamWriter(outputPath, false, System.Text.Encoding.UTF8);

            foreach (var dllPath in dlls)
            {
                if (!System.IO.File.Exists(dllPath))
                {
                    writer.WriteLine("Missing: " + dllPath);
                    writer.WriteLine();
                    continue;
                }

                Console.WriteLine("Loading: " + dllPath);
                var asm = AssemblyDefinition.ReadAssembly(dllPath);
                writer.WriteLine("Assembly: " + asm.Name.Name + " - " + asm.MainModule.Types.Count + " types");

                foreach (var t in asm.MainModule.Types.OrderBy(t => t.FullName))
                {
                    writer.WriteLine("\nType: " + t.FullName);

                    foreach (var m in t.Methods.OrderBy(m => m.Name))
                    {
                        var parameters = string.Join(", ", m.Parameters.Select(p => p.ParameterType.FullName));
                        writer.WriteLine($"  Method: {m.ReturnType.FullName} {m.Name}({parameters})");
                    }
                }

                writer.WriteLine();
            }

            writer.WriteLine("\nDump complete.");
            Console.WriteLine("Wrote: " + outputPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
