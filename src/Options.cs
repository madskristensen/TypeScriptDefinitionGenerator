using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using System.ComponentModel;
using System.IO;

namespace TypeScriptDefinitionGenerator
{
    public class OptionsDialogPage : DialogPage
    {
        private string _defaultModuleName = "server";

        [Category("Casing")]
        [DisplayName("Camel case enum values")]
        [DefaultValue(true)]
        public bool CamelCaseEnumerationValues { get; set; } = true;

        [Category("Casing")]
        [DisplayName("Camel case property names")]
        [DefaultValue(true)]
        public bool CamelCasePropertyNames { get; set; } = true;

        [Category("Casing")]
        [DisplayName("Camel case type names")]
        [DefaultValue(true)]
        public bool CamelCaseTypeNames { get; set; } = true;

        [Category("Settings")]
        [DisplayName("Default Module name")]
        [Description("Set the top-level module name for the generated .d.ts file. Default is \"server\"")]
        public string DefaultModuleName
        {
            get { return _defaultModuleName; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _defaultModuleName = value;
                }
                else
                {
                    _defaultModuleName = "server";
                }
            }
        }

        [Category("Settings")]
        [DisplayName("Class instead of Interface")]
        [Description("Controls whether to generate a class or an interface: default is an Interface")]
        [DefaultValue(false)]
        public bool ClassInsteadOfInterface { get; set; } = false;

        [Category("Settings")]
        [DisplayName("Generate in global scope")]
        [Description("Controls whether to generate types in Global scope or wrapped in a module")]
        [DefaultValue(false)]
        public bool GlobalScope { get; set; } = false;


        [Category("Compatibilty")]
        [DisplayName("Web Esentials 2015 file names")]
        [Description("Web Essentials 2015 format is <filename>.cs.d.ts instead of <filename>.d.ts")]
        [DefaultValue(true)]
        public bool WebEssentials2015 { get; set; } = true;
    }

    public class Options
    {
        static OptionsOverride overrides { get; set; } = null;
        static public bool CamelCaseEnumerationValues
        {
            get
            {
                if (overrides != null && overrides.CamelCaseEnumerationValues != null)
                {
                    return overrides.CamelCaseEnumerationValues.Value;
                }
                return DtsPackage.Options.CamelCaseEnumerationValues;
            }
        }

        static public bool CamelCasePropertyNames
        {
            get
            {
                if (overrides != null && overrides.CamelCasePropertyNames != null)
                {
                    return overrides.CamelCasePropertyNames.Value;
                }
                return DtsPackage.Options.CamelCasePropertyNames;
            }
        }

        static public bool CamelCaseTypeNames
        {
            get
            {
                if (overrides != null && overrides.CamelCaseTypeNames != null)
                {
                    return overrides.CamelCaseTypeNames.Value;
                }
                return DtsPackage.Options.CamelCaseTypeNames;
            }
        }

        static public string DefaultModuleName
        {
            get
            {
                if (overrides != null && overrides.DefaultModuleName != null)
                {
                    return overrides.DefaultModuleName;
                }
                return DtsPackage.Options.DefaultModuleName;
            }
        }

        static public bool ClassInsteadOfInterface
        {
            get
            {
                if (overrides != null && overrides.ClassInsteadOfInterface != null)
                {
                    return overrides.ClassInsteadOfInterface.Value;
                }
                return DtsPackage.Options.ClassInsteadOfInterface;
            }
        }

        static public bool GlobalScope
        {
            get
            {
                if (overrides != null && overrides.GlobalScope != null)
                {
                    return overrides.GlobalScope.Value;
                }
                return DtsPackage.Options.GlobalScope;
            }
        }

        static public bool WebEssentials2015
        {
            get
            {
                if (overrides != null && overrides.WebEssentials2015 != null)
                {
                    return overrides.WebEssentials2015.Value;
                }
                return DtsPackage.Options.WebEssentials2015;
            }
        }
        public static void ReadOptionOverrides(ProjectItem sourceItem)
        {
            overrides = null;
            Project proj = sourceItem.ContainingProject;
            foreach (ProjectItem item in proj.ProjectItems)
            {
                if (item.Name.ToLower() == "tsdefgen.json")
                {
                    try
                    {
                        overrides = JsonConvert.DeserializeObject<OptionsOverride>(File.ReadAllText(item.FileNames[0]));
                    }
                    catch (Newtonsoft.Json.JsonReaderException e)
                    {
                        VSHelpers.WriteOnOutputWindow(e.Message);
                        throw;
                    }

                    break;
                }
            }
        }

    }

    internal class OptionsOverride
    {
        public bool? CamelCaseEnumerationValues { get; set; }

        public bool? CamelCasePropertyNames { get; set; }

        public bool? CamelCaseTypeNames { get; set; }

        public string DefaultModuleName { get; set; }

        public bool? ClassInsteadOfInterface { get; set; }

        public bool? GlobalScope { get; set; }

        public bool? WebEssentials2015 { get; set; }

    }

}
