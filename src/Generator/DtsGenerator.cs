using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                    Telemetry.TrackOperation("FileGenerated");

                    InsertToIndex(item, Path.GetFileNameWithoutExtension(inputFileName));

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

        private void InsertToIndex(ProjectItem item, string inputFileName)
        {
            string projectPath = Path.GetDirectoryName(item.ContainingProject.FileName);
            string angularAppPath = Path.Combine(projectPath, "ClientApp", "src", "app");
            string angularModelDirPath = Path.Combine(angularAppPath, "models");
            string angularModelindexTsPath = Path.Combine(angularModelDirPath, "index.ts");            
            Uri toUri = new Uri(item.Document.Path);
            Uri fromUri = new Uri(angularModelDirPath + "/");
            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Path.Combine(
                Uri.UnescapeDataString(relativeUri.ToString()), 
                inputFileName);

            if (Directory.Exists(angularAppPath))
            {
                if (!Directory.Exists(angularModelDirPath))
                    Directory.CreateDirectory(angularModelDirPath);
                if (!File.Exists(angularModelindexTsPath))
                {
                    File.WriteAllText(angularModelindexTsPath, $"export * from '{relativePath}';\n");
                }
                else
                {
                    List<string> lines = File.ReadAllLines(angularModelindexTsPath).ToList();
                    if (!lines.Any( l => l.Contains(inputFileName)))
                    {
                        lines.Add($"export * from '{relativePath}';\n");
                        File.WriteAllLines(angularModelindexTsPath, lines.ToArray());
                    }
                }

            }
        }
    }
}
