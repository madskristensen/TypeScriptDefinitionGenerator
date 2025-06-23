using EnvDTE;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Windows.Threading;

namespace TypeScriptDefinitionGenerator
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("csharp")]
    [ContentType("basic")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class GenerationService : IWpfTextViewCreationListener
    {
        private ProjectItem _item;

        [Import]
        public ITextDocumentFactoryService _documentService { get; set; }

        public void TextViewCreated(IWpfTextView textView)
        {
            if (!_documentService.TryGetTextDocument(textView.TextBuffer, out var doc))
                return;

            _item = VSHelpers.GetProjectItem(doc.FilePath);

            if (_item?.ContainingProject == null ||
                !_item.ContainingProject.IsKind(ProjectTypes.DOTNET_Core, ProjectTypes.ASPNET_5, ProjectTypes.WEBSITE_PROJECT))
                return;

            doc.FileActionOccurred += FileActionOccurred;
        }

        private void FileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (e.FileActionType != FileActionTypes.ContentSavedToDisk) return;

            _item = VSHelpers.GetProjectItem(e.FilePath);
            if (_item == null) return;

            Options.ReadOptionOverrides(_item, false);
            string fileName = GenerateFileName(_item);

            // Only auto-generate on save if the file is already being watched.
            if (File.Exists(fileName))
            {
                DtsPackage.EnsurePackageLoad();
                GenerateFromProjectItem(_item); // MODIFIED: Call the new orchestrator
            }
        }

        public static string GenerateFromProjectItem(ProjectItem sourceItem)
        {
            string primaryTsContent = null;
            try
            {
                Options.ReadOptionOverrides(sourceItem);
                VSHelpers.WriteOnOutputWindow($"'{sourceItem.Name}' - Generation started...");

                var allGeneratedObjects = IntellisenseParser.ProcessFile(sourceItem);

                foreach (var fileAndObjects in allGeneratedObjects)
                {
                    var item = fileAndObjects.Key;
                    var objects = fileAndObjects.Value;

                    if (!objects.Any()) continue;

                    string sourcePath = item.FileNames[1];
                    string sourceHash = IntellisenseParser.CalculateFileHash(sourcePath);
                    string tsContent = IntellisenseWriter.WriteTypeScript(objects, sourceHash);

                    if (item == sourceItem)
                    {
                        primaryTsContent = tsContent;
                    }

                    WriteAndSyncDtsFile(item, tsContent);
                }
                VSHelpers.WriteOnOutputWindow($"'{sourceItem.Name}' - Generation and all dependencies processed successfully.");
            }
            catch (Exception ex)
            {
                // ...
            }
            return primaryTsContent ?? "";
        }

        private static void WriteAndSyncDtsFile(ProjectItem sourceItem, string tsContent)
        {
            string dtsFile = GenerateFileName(sourceItem);
            VSHelpers.WriteOnOutputWindow($"   -> Writing and syncing {Path.GetFileName(dtsFile)}");

            // File system operations can happen on any thread.
            string directory = Path.GetDirectoryName(dtsFile);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            VSHelpers.CheckFileOutOfSourceControl(dtsFile);
            File.WriteAllText(dtsFile, tsContent);

            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                EnableSyncForProjectItem(sourceItem);
            }), DispatcherPriority.ApplicationIdle, null);
        }

        public static void EnableSyncForProjectItem(ProjectItem csItem)
        {
            if (csItem == null) return;

            string dtsFile = GenerateFileName(csItem);
            string dtsFileName = Path.GetFileName(dtsFile);
            Project project = csItem.ContainingProject; // Get the parent project

            // 1. Add the generated file to the project's ROOT if it's not already there.
            // THIS IS THE KEY CHANGE. We always add to the project, not the csItem.
            if (VSHelpers.GetProjectItem(dtsFile) == null)
            {
                project.ProjectItems.AddFromFile(dtsFile);
            }

            // 2. Get a handle to the generated .d.ts project item.
            var dtsItem = VSHelpers.GetProjectItem(dtsFile);

            // 3. Set properties on the C# source item. (This part is correct)
            try
            {
                var customToolProp = csItem.Properties.Item("CustomTool");
                if (customToolProp.Value?.ToString() != DtsGenerator.Name)
                {
                    customToolProp.Value = DtsGenerator.Name;
                }

                var lastGenOutputProp = csItem.Properties.Item("LastGenOutput");
                if (lastGenOutputProp.Value?.ToString() != dtsFileName)
                {
                    lastGenOutputProp.Value = dtsFileName;
                }
            }
            catch (Exception ex)
            {
                VSHelpers.WriteOnOutputWindow($"Warning: Could not set generator properties on '{csItem.Name}'. {ex.Message}");
            }

            // 4. Set properties on the .d.ts generated item for nesting. (This part is correct)
            if (dtsItem != null)
            {
                try
                {
                    // The DependentUpon property is what creates the visual nesting in Solution Explorer.
                    // It works correctly even if the files are in different directories.
                    var dependentUponProp = dtsItem.Properties.Item("DependentUpon");
                    if (dependentUponProp.Value?.ToString() != csItem.Name)
                    {
                        dependentUponProp.Value = csItem.Name;
                    }

                    dtsItem.Properties.Item("DesignTime").Value = true;
                    dtsItem.Properties.Item("AutoGen").Value = true;
                }
                catch (Exception ex)
                {
                    VSHelpers.WriteOnOutputWindow($"Warning: Could not set nesting properties for '{dtsFileName}'. {ex.Message}");
                }
            }
        }

        public static void DisableSyncForProjectItem(ProjectItem csItem)
        {
            if (csItem == null) return;
            string dtsFile = GenerateFileName(csItem);

            // 1. Delete the generated file from the project and disk.
            VSHelpers.GetProjectItem(dtsFile)?.Delete();
            if (File.Exists(dtsFile))
            {
                try { File.Delete(dtsFile); } catch { /* Ignore */ }
            }

            // 2. Clear the properties on the C# source item.
            try
            {
                csItem.Properties.Item("CustomTool").Value = "";
                csItem.Properties.Item("LastGenOutput").Value = "";
                VSHelpers.WriteOnOutputWindow($"Sync disabled for '{csItem.Name}'.");
            }
            catch (Exception ex)
            {
                VSHelpers.WriteOnOutputWindow($"Warning: Could not clear generator properties on '{csItem.Name}'. {ex.Message}");
            }
        }

        public static string GenerateFileName(ProjectItem sourceItem)
        {
            string sourceFile = sourceItem.FileNames[1];
            string projectRoot = Options.GetProjectRoot(sourceItem.ContainingProject);
            string outputPath = Options.OutputPath;

            string dtsFileName = Path.GetFileName(sourceFile);
            if (Options.WebEssentials2015)
            {
                dtsFileName += Constants.FileExtension;
            }
            else
            {
                dtsFileName = Path.ChangeExtension(dtsFileName, Constants.FileExtension);
            }

            if (string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrEmpty(projectRoot))
            {
                // Original behavior: save next to the source file
                return Path.Combine(Path.GetDirectoryName(sourceFile), dtsFileName);
            }

            // New behavior: save to custom output path, preserving folder structure
            Uri sourceUri = new Uri(sourceFile);
            Uri projectRootUri = new Uri(projectRoot + Path.DirectorySeparatorChar);
            string relativePath = Uri.UnescapeDataString(projectRootUri.MakeRelativeUri(sourceUri).ToString());
            string relativeDir = Path.GetDirectoryName(relativePath);

            string finalOutputDir = Path.Combine(projectRoot, outputPath, relativeDir);

            return Path.Combine(finalOutputDir, dtsFileName);
        }
    }
}
