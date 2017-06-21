using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace TypeScriptDefinitionGenerator
{
    public class Options : DialogPage
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
}
