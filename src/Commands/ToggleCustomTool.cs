using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;

namespace TypeScriptDefinitionGenerator
{
    internal sealed class ToggleCustomTool
    {
        private readonly Package _package;
        private string[] _extesions = { ".cs", ".vb" };
        private ProjectItem _item;

        private ToggleCustomTool(Package package, OleMenuCommandService commandService)
        {
            _package = package;

            var menuCommandID = new CommandID(PackageGuids.guidDtsPackageCmdSet, PackageIds.ToggleCustomToolId);
            var menuItem = new OleMenuCommand(Execute, menuCommandID);
            menuItem.BeforeQueryStatus += BeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }

        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            var button = (OleMenuCommand)sender;
            button.Visible = button.Enabled = false;

            var dte = ServiceProvider.GetService(typeof(DTE)) as DTE2;

            if (dte.SelectedItems.Count != 1)
                return;

            _item = dte.SelectedItems?.Item(1)?.ProjectItem;

            if (_item == null)
                return;

            var fileName = _item.FileNames[1];
            var ext = Path.GetExtension(fileName);

            if (_extesions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                button.Checked = _item.Properties.Item("CustomTool").Value.ToString() == DtsGenerator.Name;
                button.Visible = button.Enabled = true;
            }
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

        public static void Initialize(Package package, OleMenuCommandService commandService)
        {
            Instance = new ToggleCustomTool(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            bool synOn = _item.Properties.Item("CustomTool").Value.ToString() == DtsGenerator.Name;

            if (synOn)
            {
                _item.Properties.Item("CustomTool").Value = "";
            }
            else
            {
                _item.Properties.Item("CustomTool").Value = DtsGenerator.Name;
            }
        }
    }
}
