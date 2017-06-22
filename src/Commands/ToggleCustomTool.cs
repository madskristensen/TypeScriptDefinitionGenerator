using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using Tasks = System.Threading.Tasks;

namespace TypeScriptDefinitionGenerator
{
    internal sealed class ToggleCustomTool
    {
        private readonly Package _package;
        private ProjectItem _item;
        private DTE2 _dte;

        private ToggleCustomTool(Package package, OleMenuCommandService commandService, DTE2 dte)
        {
            _package = package;
            _dte = dte;

            var cmdId = new CommandID(PackageGuids.guidDtsPackageCmdSet, PackageIds.ToggleCustomToolId);
            var cmd = new OleMenuCommand(Execute, cmdId);
            cmd.BeforeQueryStatus += BeforeQueryStatus;
            commandService.AddCommand(cmd);
        }

        public static ToggleCustomTool Instance
        {
            get;
            private set;
        }

        private IServiceProvider ServiceProvider
        {
            get { return _package; }
        }

        public static async Tasks.Task InitializeAsync(AsyncPackage package)
        {
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;

            Instance = new ToggleCustomTool(package, commandService, dte);
        }

        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            var button = (OleMenuCommand)sender;
            button.Visible = button.Enabled = false;

            if (_dte.SelectedItems.Count != 1)
                return;

            _item = _dte.SelectedItems?.Item(1)?.ProjectItem;
            Options.ReadOptionOverrides(_item, false);

            if (_item == null || _item.ContainingProject == null || _item.FileCodeModel == null)
                return;

            var fileName = _item.FileNames[1];
            var ext = Path.GetExtension(fileName);

            if (Constants.SupportedSourceExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                if (_item.ContainingProject.IsKind(ProjectTypes.DOTNET_Core, ProjectTypes.ASPNET_5, ProjectTypes.WEBSITE_PROJECT))
                {
                    string dtsFile = GenerationService.GenerateFileName(_item.FileNames[1]);
                    button.Checked = File.Exists(dtsFile);
                }
                else
                {
                    button.Checked = _item.Properties.Item("CustomTool").Value.ToString() == DtsGenerator.Name;
                }

                button.Visible = button.Enabled = true;
            }
        }

        private void Execute(object sender, EventArgs e)
        {
            Options.ReadOptionOverrides(_item, false);
            // .NET Core and Website projects
            if (_item.ContainingProject.IsKind(ProjectTypes.DOTNET_Core, ProjectTypes.ASPNET_5, ProjectTypes.WEBSITE_PROJECT))
            {
                string dtsFile = GenerationService.GenerateFileName(_item.FileNames[1]);
                bool synOn = File.Exists(dtsFile);

                if (synOn)
                {
                    var dtsItem = VSHelpers.GetProjectItem(dtsFile);
                    dtsItem?.Delete();
                    File.Delete(dtsFile);
                }
                else
                {
                    GenerationService.CreateDtsFile(_item);
                }
            }
            // Legacy .NET projects
            else
            {
                bool synOn = _item.Properties.Item("CustomTool").Value.ToString() == DtsGenerator.Name;

                if (synOn)
                {
                    _item.Properties.Item("CustomTool").Value = "";
                    Telemetry.TrackUserTask("EnableGeneration");
                }
                else
                {
                    _item.Properties.Item("CustomTool").Value = DtsGenerator.Name;
                    Telemetry.TrackUserTask("DisabledGeneration");
                }
            }
        }
    }
}
