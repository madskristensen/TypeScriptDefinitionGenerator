using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;

namespace TypeScriptDefinitionGenerator
{
    [Guid(PackageGuids.guidDtsPackageString)]
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideOptionPage(typeof(Options), "Text Editor\\JavaScript/TypeScript", "Generate d.ts", 101, 102, true, new[] { "d.ts" }, ProvidesLocalizedCategoryName = false)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideCodeGenerator(typeof(DtsGenerator), DtsGenerator.Name, DtsGenerator.Desription, true)]
    [ProvideAutoLoad(PackageGuids.UIContextRuleString)]
    [ProvideUIContextRule(PackageGuids.UIContextRuleString,
        name: "Auto load",
        expression: "cs | vb | csct | vbct",
        termNames: new[] { "cs", "vb", "csct", "vbct" },
        termValues: new[] { "HierSingleSelectionName:.cs$", "HierSingleSelectionName:.vb$", "ActiveEditorContentType:csharp", "ActiveEditorContentType:basic" })]
    public sealed class DtsPackage : AsyncPackage
    {
        public static Options Options
        {
            get;
            private set;
        }

        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Options = (Options)GetDialogPage(typeof(Options));

            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            ToggleCustomTool.Initialize(this, commandService);
        }
    }
}
