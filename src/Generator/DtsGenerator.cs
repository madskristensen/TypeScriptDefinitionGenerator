using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.TextTemplating.VSHost;

namespace TypeScriptDefinitionGenerator
{
    [Guid("d1e92907-20ee-4b6f-ba64-142297def4e4")]
    public sealed class DtsGenerator : BaseCodeGeneratorWithSite
    {
        public const string Name = nameof(DtsGenerator);
        public const string Description = "Automatically generates the .d.ts file based on the C#/VB model class.";

        private string originalExt { get; set; }

        public override string GetDefaultExtension()
        {
            if (Options.WebEssentials2015)
            {
                return originalExt + Constants.FileExtension;
            }
            else
            {
                return Constants.FileExtension;
            }
        }

        protected override byte[] GenerateCode(string inputFileName, string inputFileContent)
        {
            ProjectItem item = (Dte as DTE2).Solution.FindProjectItem(inputFileName);
            originalExt = Path.GetExtension(inputFileName);
            if (item != null)
            {
                try
                {
                    var dts = GenerationService.ConvertToTypeScript(item);

                    return Encoding.UTF8.GetBytes(dts);
                }
                catch (Exception ex)
                { }
                    
            }

            return new byte[0];
        }
    }
}
