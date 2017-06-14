using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.IO;

namespace TypeScriptDefinitionGenerator
{
    public static class VSHelpers
    {
        private static DTE2 DTE { get; } = Package.GetGlobalService(typeof(DTE)) as DTE2;

        public static ProjectItem GetProjectItem(string fileName)
        {
            return DTE.Solution.FindProjectItem(fileName);
        }

        public static void CheckFileOutOfSourceControl(string file)
        {
            if (!File.Exists(file) || DTE.Solution.FindProjectItem(file) == null)
                return;

            if (DTE.SourceControl.IsItemUnderSCC(file) && !DTE.SourceControl.IsItemCheckedOut(file))
                DTE.SourceControl.CheckOutItem(file);

            var info = new FileInfo(file)
            {
                IsReadOnly = false
            };
        }

        public static bool IsKind(this Project project, params string[] kindGuids)
        {
            foreach (var guid in kindGuids)
            {
                if (project.Kind.Equals(guid, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static readonly Guid outputPaneGuid = new Guid();

        internal static void WriteOnOutputWindow(string text)
        {
            WriteOnOutputWindow("TypeScript Definition Generator: " + text, outputPaneGuid);
        }
        internal static void WriteOnBuildOutputWindow(string text)
        {
            WriteOnOutputWindow(text, Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid);
        }

        internal static void WriteOnOutputWindow(string text, Guid guidBuildOutput)
        {
            if (!text.EndsWith(Environment.NewLine))
            {
                text += Environment.NewLine;
            }

            // At first write the text on the debug output.
            Debug.Write(text);

            // Now get the SVsOutputWindow service from the service provider.
            IVsOutputWindow outputWindow = ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (null == outputWindow)
            {
                // If the provider doesn't expose the service there is nothing we can do.
                // Write a message on the debug output and exit.
                Debug.WriteLine("Can not get the SVsOutputWindow service.");
                return;
            }

            // We can not write on the Output window itself, but only on one of its panes.
            // Here we try to use the "General" pane.
            IVsOutputWindowPane windowPane;
            if (Microsoft.VisualStudio.ErrorHandler.Failed(outputWindow.GetPane(ref guidBuildOutput, out windowPane)) ||
                (null == windowPane))
            {
                if (Microsoft.VisualStudio.ErrorHandler.Failed(outputWindow.CreatePane(ref guidBuildOutput, "TypeScript Definition Generator", 1, 0)))
                {
                    // Nothing to do here, just debug output and exit
                    Debug.WriteLine("Failed to create the Output window pane.");
                    return;
                }
                if (Microsoft.VisualStudio.ErrorHandler.Failed(outputWindow.GetPane(ref guidBuildOutput, out windowPane)) ||
                (null == windowPane))
                {
                    // Again, there is nothing we can do to recover from this error, so write on the
                    // debug output and exit.
                    Debug.WriteLine("Failed to get the Output window pane.");
                    return;
                }
            }
            if (Microsoft.VisualStudio.ErrorHandler.Failed(windowPane.Activate()))
            {
                Debug.WriteLine("Failed to activate the Output window pane.");
                return;
            }

            // Finally we can write on the window pane.
            if (Microsoft.VisualStudio.ErrorHandler.Failed(windowPane.OutputString(text)))
            {
                Debug.WriteLine("Failed to write on the Output window pane.");
            }
        }
    }

    public static class ProjectTypes
    {
        public const string ASPNET_5 = "{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}";
        public const string DOTNET_Core = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
        public const string WEBSITE_PROJECT = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";
        public const string UNIVERSAL_APP = "{262852C6-CD72-467D-83FE-5EEB1973A190}";
        public const string NODE_JS = "{9092AA53-FB77-4645-B42D-1CCCA6BD08BD}";
        public const string SSDT = "{00d1a9c2-b5f0-4af3-8072-f6c62b433612}";
    }
}
