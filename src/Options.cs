using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;

namespace TypeScriptDefinitionGenerator
{
    public class OptionsDialogPage : DialogPage
    {
        internal const bool _defCamelCaseEnumerationValues = true;
        internal const bool _defCamelCasePropertyNames = true;
        internal const bool _defCamelCaseTypeNames = true;
        internal const bool _defClassInsteadOfInterface = true;
        internal const bool _defStringInsteadOfEnum = false;
        internal const bool _defGlobalScope = true;
        internal const bool _defWebEssentials2015 = false;
        internal const string _defModuleName = "server";

        [Category("Casing")]
        [DisplayName("Camel case enum values")]
        [DefaultValue(_defCamelCaseEnumerationValues)]
        public bool CamelCaseEnumerationValues { get; set; } = _defCamelCaseEnumerationValues;

        [Category("Casing")]
        [DisplayName("Camel case property names")]
        [DefaultValue(_defCamelCasePropertyNames)]
        public bool CamelCasePropertyNames { get; set; } = _defCamelCasePropertyNames;

        [Category("Casing")]
        [DisplayName("Camel case type names")]
        [DefaultValue(_defCamelCaseTypeNames)]
        public bool CamelCaseTypeNames { get; set; } = _defCamelCaseTypeNames;

        [Category("Settings")]
        [DisplayName("Default Module name")]
        [Description("Set the top-level module name for the generated .d.ts file. Default is \"server\"")]
        public string DefaultModuleName { get; set; } = _defModuleName;

        [Category("Settings")]
        [DisplayName("Class instead of Interface")]
        [Description("Controls whether to generate a class or an interface: default is an Interface")]
        [DefaultValue(_defClassInsteadOfInterface)]
        public bool ClassInsteadOfInterface { get; set; } = _defClassInsteadOfInterface;

        [Category("Settings")]
        [DisplayName("String enumeration instead of Enum")]
        [Description("Controls whether to generate an enum or a string ('a' | 'b' | 'c'): default is an Interface")]
        [DefaultValue(_defStringInsteadOfEnum)]
        public bool StringInsteadOfEnum { get; set; } = _defStringInsteadOfEnum;

        [Category("Settings")]
        [DisplayName("Generate in global scope")]
        [Description("Controls whether to generate types in Global scope or wrapped in a module")]
        [DefaultValue(_defGlobalScope)]
        public bool GlobalScope { get; set; } = _defGlobalScope;


        [Category("Compatibilty")]
        [DisplayName("Web Essentials 2015 file names")]
        [Description("Web Essentials 2015 format is <filename>.cs.d.ts instead of <filename>.d.ts")]
        [DefaultValue(_defWebEssentials2015)]
        public bool WebEssentials2015 { get; set; } = _defWebEssentials2015;
    }

    public class Options
    {
        const string OVERRIDE_FILE_NAME = "tsdefgen.json";
        static OptionsOverride overrides { get; set; } = null;
        static public bool CamelCaseEnumerationValues
        {
            get
            {
                return overrides != null ? overrides.CamelCaseEnumerationValues : DtsPackage.Options.CamelCaseEnumerationValues;
            }
        }

        static public bool CamelCasePropertyNames
        {
            get
            {
                return overrides != null ? overrides.CamelCasePropertyNames : DtsPackage.Options.CamelCasePropertyNames;
            }
        }

        static public bool CamelCaseTypeNames
        {
            get
            {
                return overrides != null ? overrides.CamelCaseTypeNames : DtsPackage.Options.CamelCaseTypeNames;
            }
        }

        static public string DefaultModuleName
        {
            get
            {
                return overrides != null ? overrides.DefaultModuleName : DtsPackage.Options.DefaultModuleName;
            }
        }

        static public bool ClassInsteadOfInterface
        {
            get
            {
                return overrides != null ? overrides.ClassInsteadOfInterface : DtsPackage.Options.ClassInsteadOfInterface;
            }
        }

        static public bool StringInsteadOfEnum
        {
            get
            {
                return overrides != null ? overrides.StringInsteadOfEnum : DtsPackage.Options.StringInsteadOfEnum;
            }
        }

        static public bool GlobalScope
        {
            get
            {
                return overrides != null ? overrides.GlobalScope : DtsPackage.Options.GlobalScope;
            }
        }

        static public bool WebEssentials2015
        {
            get
            {
                return overrides != null ? overrides.WebEssentials2015 : DtsPackage.Options.WebEssentials2015;
            }
        }

        public static void ReadOptionOverrides(ProjectItem sourceItem, bool display = true)
        {
            Project proj = sourceItem.ContainingProject;

            string jsonName = "";

            foreach (ProjectItem item in proj.ProjectItems)
            {
                if (item.Name.ToLower() == OVERRIDE_FILE_NAME.ToLower())
                {
                    jsonName = item.FileNames[0];
                    break;
                }
            }

            if (!string.IsNullOrEmpty(jsonName))
            {
                // it has been modified since last read - so read again
                try
                {
                    overrides = JsonConvert.DeserializeObject<OptionsOverride>(File.ReadAllText(jsonName));
                    if (display)
                    {
                        VSHelpers.WriteOnOutputWindow(string.Format("Override file processed: {0}", jsonName));
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Override file processed: {0}", jsonName));
                    }
                }
                catch (Exception e) when (e is Newtonsoft.Json.JsonReaderException || e is Newtonsoft.Json.JsonSerializationException)
                {
                    overrides = null; // incase the read fails
                    VSHelpers.WriteOnOutputWindow(string.Format("Error in Override file: {0}", jsonName));
                    VSHelpers.WriteOnOutputWindow(e.Message);
                    throw;
                }
            }
            else
            {
                if (display)
                {
                    VSHelpers.WriteOnOutputWindow("Using Global Settings");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Using Global Settings");
                }
                overrides = null;
            }
        }

    }

    internal class OptionsOverride
    {
        //        [JsonRequired]
        public bool CamelCaseEnumerationValues { get; set; } = OptionsDialogPage._defCamelCaseEnumerationValues;

        //        [JsonRequired]
        public bool CamelCasePropertyNames { get; set; } = OptionsDialogPage._defCamelCasePropertyNames;

        //        [JsonRequired]
        public bool CamelCaseTypeNames { get; set; } = OptionsDialogPage._defCamelCaseTypeNames;

        //        [JsonRequired]
        public string DefaultModuleName { get; set; } = OptionsDialogPage._defModuleName;

        //        [JsonRequired]
        public bool ClassInsteadOfInterface { get; set; } = OptionsDialogPage._defClassInsteadOfInterface;

        //        [JsonRequired]
        public bool StringInsteadOfEnum { get; set; } = OptionsDialogPage._defStringInsteadOfEnum;

        //        [JsonRequired]
        public bool GlobalScope { get; set; } = OptionsDialogPage._defGlobalScope;

        //        [JsonRequired]
        public bool WebEssentials2015 { get; set; } = OptionsDialogPage._defWebEssentials2015;

    }

}
