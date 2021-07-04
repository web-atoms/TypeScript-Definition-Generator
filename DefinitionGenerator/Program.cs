using System;
using System.Reflection;

namespace DefinitionGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length < 3)
            {
                Console.WriteLine($"Usage: DefinitionGenerator tsFileName rootClrNameSpace dllFilePath");
                Console.WriteLine("Example: DefinitionGenerator XF http://xamarin.com/schemas/2014/forms d:/{absolute-path}/Xamarin.Forms.Core.dll");
                Console.WriteLine("Example: DefinitionGenerator WA WebAtoms ../../..{relative-path}/../WebAtoms.XF.dll WA");
                return;
            }

            Generate(args[0], args[1], args[2]);
        }

        private static void Generate(string name, string @namespace, string filePath)
        {
            if (name.EndsWith(".ts"))
            {
                name = name.Substring(0, name.Length - 3);
                Generate(name, @namespace, filePath);
                return;
            }

            var output = name + ".ts";

            var g = new Generator(name, @namespace, filePath);
            System.IO.File.WriteAllText(output, g.Generate(), System.Text.Encoding.UTF8);
        }
    }
}
