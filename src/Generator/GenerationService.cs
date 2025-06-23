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

            try
            {
                string dtsFile = GenerateFileName(csItem); // This is the absolute physical path
                Project project = csItem.ContainingProject;
                string projectRoot = Options.GetProjectRoot(project);

                string csSourcePath = csItem.FileNames[1];
                string csRelativePath = csSourcePath;
                if (!string.IsNullOrEmpty(projectRoot) && csSourcePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    csRelativePath = csSourcePath.Substring(projectRoot.Length).TrimStart('\\', '/');
                }

                string dtsLinkPath = Path.ChangeExtension(csRelativePath, Constants.FileExtension);
                if (Options.WebEssentials2015)
                    dtsLinkPath = csRelativePath + Constants.FileExtension;

                // Get a handle to the .d.ts project item.
                //var dtsItem = VSHelpers.GetProjectItem(dtsFile);

                //// 1. Add the generated file to the project's ROOT if it's not already there.
                //if (dtsItem == null)
                //{
                //    dtsItem = project.ProjectItems.AddFromFile(dtsFile);
                //}

                //// 2. Calculate the source file's path relative to the project root.
                //// This will be used for the <Link> metadata and DependentUpon.
            

                //// 3. Set properties on the .d.ts generated item.
                //if (dtsItem != null)
                //{
                //    try
                //    {
                //        // Set the <Link> property to preserve the folder structure in Solution Explorer.
                //        // We want the linked file to appear at the same relative path as the source .cs file.
                    

                //        dtsItem.Properties.Item("Link").Value = dtsLinkPath;

                //        // Set DependentUpon using the relative path of the source .cs file.
                //        dtsItem.Properties.Item("DependentUpon").Value = csRelativePath;

                //        // These properties set <DesignTime> and <AutoGen>.
                //        dtsItem.Properties.Item("DesignTime").Value = true;
                //        dtsItem.Properties.Item("AutoGen").Value = true;
                //    }
                //    catch (Exception ex)
                //    {
                //        VSHelpers.WriteOnOutputWindow($"Warning: Could not set nesting/link properties for '{Path.GetFileName(dtsFile)}'. {ex.Message}");
                //    }
                //}

            // 4. Set properties on the C# source item.          
                csItem.Properties.Item("CustomTool").Value = DtsGenerator.Name;

                // Set LastGenOutput to the physical path of the .d.ts file.
                // For SDK projects, this should be the full path that appears in the <None Include="..."> tag.
                string dtsRelativePathForInclude = dtsFile;
                if (!string.IsNullOrEmpty(projectRoot) && dtsFile.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    dtsRelativePathForInclude = dtsFile.Substring(projectRoot.Length).TrimStart('\\', '/');
                }
                else
                {
                    // If outside the project, create a relative path from the project file location
                    Uri projectUri = new Uri(project.FullName);
                    Uri fileUri = new Uri(dtsFile);
                    dtsRelativePathForInclude = Uri.UnescapeDataString(projectUri.MakeRelativeUri(fileUri).ToString()).Replace('/', '\\');
                }

                csItem.Properties.Item("LastGenOutput").Value = dtsRelativePathForInclude;
            }
            catch (Exception ex)
            {
                VSHelpers.WriteOnOutputWindow($"Warning: Could not set generator properties on '{csItem.Name}'. {ex.Message}");
            }
        }

        public static void DisableSyncForProjectItem(ProjectItem csItem)
        {
            if (csItem == null) return;

            // Use LastGenOutput to find the exact path of the file to delete, as this is the most reliable source.
            string dtsFileToDelete = "";
            try
            {
                dtsFileToDelete = csItem.Properties.Item("LastGenOutput").Value.ToString();
                if (!Path.IsPathRooted(dtsFileToDelete))
                {
                    string projectDir = Path.GetDirectoryName(csItem.ContainingProject.FullName);
                    dtsFileToDelete = Path.GetFullPath(Path.Combine(projectDir, dtsFileToDelete));
                }
            }
            catch
            {
                // Fallback if LastGenOutput isn't set for some reason.
                dtsFileToDelete = GenerateFileName(csItem);
            }

            // 1. Delete the generated file from the project and disk.
            VSHelpers.GetProjectItem(dtsFileToDelete)?.Delete();
            if (File.Exists(dtsFileToDelete))
            {
                try { File.Delete(dtsFileToDelete); } catch { /* Ignore */ }
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

            string finalPath; // Use a variable to hold the result

            if (string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrEmpty(projectRoot))
            {
                // Original behavior: save next to the source file
                finalPath = Path.Combine(Path.GetDirectoryName(sourceFile), dtsFileName);
            }
            else
            {
                // New behavior: save to custom output path, preserving folder structure
                Uri sourceUri = new Uri(sourceFile);
                Uri projectRootUri = new Uri(projectRoot + Path.DirectorySeparatorChar);
                string relativePath = Uri.UnescapeDataString(projectRootUri.MakeRelativeUri(sourceUri).ToString());
                string relativeDir = Path.GetDirectoryName(relativePath);

                string finalOutputDir = Path.Combine(projectRoot, outputPath, relativeDir);
                finalPath = Path.Combine(finalOutputDir, dtsFileName);
            }

            return Path.GetFullPath(finalPath);
        }
    }
}
