using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Tasks = System.Threading.Tasks;

namespace TypeScriptDefinitionGenerator
{
    [Guid(PackageGuids.guidDtsPackageString)]
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideLanguageEditorOptionPage(typeof(OptionsDialogPage), "TypeScript", null, "Generate d.ts", null, new[] { "d.ts" })]
    [ProvideCodeGenerator(typeof(DtsGenerator), DtsGenerator.Name, DtsGenerator.Description, true)]
    [ProvideAutoLoad(PackageGuids.UIContextRuleString)]
    [ProvideUIContextRule(PackageGuids.UIContextRuleString,
        name: "Auto load",
        expression: "cs | vb",
        termNames: new[] { "cs", "vb" },
        termValues: new[] { "HierSingleSelectionName:.cs$", "HierSingleSelectionName:.vb$" })]
    public sealed class DtsPackage : AsyncPackage
    {
        public static OptionsDialogPage Options
        {
            get;
            private set;
        }

        public static void EnsurePackageLoad()
        {
            if (Options == null)
            {
                var shell = GetGlobalService(typeof(SVsShell)) as IVsShell;
                ErrorHandler.ThrowOnFailure(shell.LoadPackage(PackageGuids.guidDtsPackage, out var ppPackage));
            }
        }

        protected override async Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Options = (OptionsDialogPage)GetDialogPage(typeof(OptionsDialogPage));

            await ToggleCustomTool.InitializeAsync(this);
        }
    }
}
