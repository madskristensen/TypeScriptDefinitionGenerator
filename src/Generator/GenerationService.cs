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

            // DTE operations MUST be marshaled back to the UI thread.
            // Using the Dispatcher is the correct way to do this.
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                Project project = sourceItem.ContainingProject;

                // Ensure the .d.ts file is in the project
                if (VSHelpers.GetProjectItem(dtsFile) == null)
                {
                    project.ProjectItems.AddFromFile(dtsFile);
                }

                // Enable sync for the source file. This is now safe because we are on the UI thread.
                EnableSyncForProjectItem(sourceItem);

                // Nest the file in Solution Explorer if applicable
                var dtsItem = VSHelpers.GetProjectItem(dtsFile);
                if (dtsItem != null && project.IsKind(ProjectTypes.DOTNET_Core, ProjectTypes.ASPNET_5) && string.IsNullOrWhiteSpace(Options.OutputPath))
                {
                    try { dtsItem.Properties.Item("DependentUpon").Value = sourceItem.Name; } catch { /* Fails safely */ }
                }
            }), DispatcherPriority.ApplicationIdle, null);
        }

        public static void EnableSyncForProjectItem(ProjectItem csItem)
        {
            if (csItem == null) return;

            // For modern .NET SDK projects, setting the CustomTool property is how we add the <Generator> metadata.
            // For legacy projects, this is the standard mechanism anyway.
            try
            {
                var customToolProp = csItem.Properties.Item("CustomTool");
                if (customToolProp != null && customToolProp.Value?.ToString() != DtsGenerator.Name)
                {
                    customToolProp.Value = DtsGenerator.Name;
                    VSHelpers.WriteOnOutputWindow($"   -> Enabled sync for '{csItem.Name}'");
                }
            }
            catch (Exception ex)
            {
                // This can fail if the property doesn't exist for some project types.
                VSHelpers.WriteOnOutputWindow($"Warning: Could not set CustomTool on '{csItem.Name}'. Sync may not be automatic. {ex.Message}");
            }
        }

        public static void DisableSyncForProjectItem(ProjectItem csItem)
        {
            if (csItem == null) return;

            string dtsFile = GenerateFileName(csItem);

            // Remove the generated file from disk and project
            var dtsProjectItem = VSHelpers.GetProjectItem(dtsFile);
            dtsProjectItem?.Delete();
            if (File.Exists(dtsFile))
            {
                try { File.Delete(dtsFile); } catch { /* Ignore errors */ }
            }

            // Unset the CustomTool property to remove the <Generator> metadata
            try
            {
                var customToolProp = csItem.Properties.Item("CustomTool");
                if (customToolProp != null && customToolProp.Value?.ToString() == DtsGenerator.Name)
                {
                    customToolProp.Value = "";
                    VSHelpers.WriteOnOutputWindow($"Sync disabled for '{csItem.Name}'.");
                }
            }
            catch (Exception ex)
            {
                VSHelpers.WriteOnOutputWindow($"Warning: Could not clear CustomTool on '{csItem.Name}'. {ex.Message}");
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
