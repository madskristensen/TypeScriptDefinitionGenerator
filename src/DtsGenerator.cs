using Microsoft.VisualStudio.TextTemplating.VSHost;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TypeScriptDefinitionGenerator
{
    [Guid("d1e92907-20ee-4b6f-ba64-142297def4e4")]
    public sealed class DtsGenerator : BaseCodeGeneratorWithSite
    {
        public const string Name = nameof(DtsGenerator);
        public const string Description = "Automatically generates the .d.ts file based on the C#/VB model class.";

        public override string GetDefaultExtension()
        {
            return ".d.ts";
        }

        protected override byte[] GenerateCode(string inputFileName, string inputFileContent)
        {
            var item = Dte.Solution.FindProjectItem(inputFileName);

            if (item != null)
            {
                var list = IntellisenseParser.ProcessFile(item);
                var dts = IntellisenseWriter.WriteTypeScript(list);

                return Encoding.UTF8.GetBytes(dts);
            }

            return new byte[0];
        }
    }
}
