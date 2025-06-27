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
            // ... (BeforeQueryStatus logic is the same and correct)
            var button = (OleMenuCommand)sender;
            button.Visible = button.Enabled = false;

            if (_dte.SelectedItems.Count != 1) return;

            _item = _dte.SelectedItems?.Item(1)?.ProjectItem;
            if (_item == null) return;

            Options.ReadOptionOverrides(_item, false);

            if (_item.ContainingProject == null || _item.FileCodeModel == null) return;

            var fileName = _item.FileNames[1];
            var ext = Path.GetExtension(fileName);

            if (Constants.SupportedSourceExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                // The 'checked' state is now unified. We just check the CustomTool property.
                try
                {
                    var customToolProp = _item.Properties.Item("CustomTool");
                    button.Checked = customToolProp?.Value?.ToString() == DtsGenerator.Name;
                }
                catch
                {
                    button.Checked = false; // Property doesn't exist
                }

                button.Visible = button.Enabled = true;
            }
        }

        private void Execute(object sender, EventArgs e)
        {
            Options.ReadOptionOverrides(_item, true);

            // SIMPLIFIED LOGIC: The logic is now the same for all project types.
            bool isSynced;
            try
            {
                var customToolProp = _item.Properties.Item("CustomTool");
                isSynced = customToolProp?.Value?.ToString() == DtsGenerator.Name;
            }
            catch { isSynced = false; }


            if (isSynced)
            {
                // Call the new helper to turn off sync.
                GenerationService.DisableSyncForProjectItem(_item);
            }
            else
            {
                // Call the main generation method. This will generate for the item
                // and all its dependencies, and it will also enable sync for all of them.
                GenerationService.GenerateFromProjectItem(_item);
            }
        }
    }
}
