using EnvDTE;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TypeScriptDefinitionGenerator
{
    [Guid("d1e92907-20ee-4b6f-ba64-142297def4e4")]
    public sealed class DtsGenerator : BaseCodeGeneratorWithSite
    {
        public const string Name = nameof(DtsGenerator);
        public const string Description = "Automatically generates the .d.ts file based on the C#/VB model class.";

        string originalExt { get; set; }

        public override string GetDefaultExtension()
        {
            if (Options.WebEssentials2015)
            {
                return this.originalExt + Constants.FileExtension;
            }
            else
            {
                return Constants.FileExtension;
            }
        }

        protected override byte[] GenerateCode(string inputFileName, string inputFileContent)
        {
            ProjectItem item = Dte.Solution.FindProjectItem(inputFileName);
            this.originalExt = Path.GetExtension(inputFileName);
            if (item != null)
            {
                try
                {
                    string dts = GenerationService.ConvertToTypeScript(item);

                    Telemetry.TrackOperation("FileGenerated");

                    return Encoding.UTF8.GetBytes(dts);
                }
                catch (Exception ex)
                {
                    Telemetry.TrackOperation("FileGenerated", Microsoft.VisualStudio.Telemetry.TelemetryResult.Failure);
                    Telemetry.TrackException("FileGenerated", ex);
                }
            }

            return new byte[0];
        }
    }
}
