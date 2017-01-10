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
    }
}
