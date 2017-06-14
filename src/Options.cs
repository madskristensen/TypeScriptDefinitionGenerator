using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace TypeScriptDefinitionGenerator
{
    public class Options : DialogPage
    {
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


        [Category("Compatibilty")]
        [DisplayName("Web Esentials 2015 file names")]
        [Description("Web Essentials 2015 format is <filename>.cs.d.ts instead of <filename>.d.ts")]
        [DefaultValue(true)]
        public bool WebEssentials2015 { get; set; } = true;
    }
}
