using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using TypeScriptDefinitionGenerator.Helpers;

namespace TypeScriptDefinitionGenerator
{
    internal static class IntellisenseWriter
    {
        private static readonly Regex _whitespaceTrimmer = new Regex(@"^\s+|\s+$|\s*[\r\n]+\s*", RegexOptions.Compiled);

        public static string WriteTypeScript(IEnumerable<IntellisenseObject> objects, string sourceDirectory)
        {
            var sb = new StringBuilder();

            foreach (IGrouping<string, IntellisenseObject> ns in objects.GroupBy(o => o.Namespace))
            {
                if (!Options.GlobalScope)
                {
                    sb.AppendFormat("declare module {0} {{\r\n", ns.Key);
                }

                foreach (IntellisenseObject io in ns)
                {
                    foreach (var reference in io.References)
                    {
                        Uri sourceUri = new Uri(reference);
                        Uri targetUri = new Uri(System.IO.Path.Combine(sourceDirectory, System.IO.Path.GetFileName(reference)));
                        string relativePath = "./" + targetUri.MakeRelativeUri(sourceUri).ToString().Replace('\\', '/') + System.IO.Path.GetFileNameWithoutExtension(reference);
                        string refClassName = "{ " + Utility.CamelCaseClassName(System.IO.Path.GetFileNameWithoutExtension(reference)) + " }";
                        sb.AppendLine($"import {refClassName} from '{relativePath}'");
                    }
                }
                
                foreach (IntellisenseObject io in ns)
                {
                    if (!string.IsNullOrEmpty(io.Summary))
                    {
                        sb.AppendLine("\t/** " + _whitespaceTrimmer.Replace(io.Summary, "") + " */");
                    }

                    if (io.IsEnum)
                    {
                        if (!Options.StringInsteadOfEnum)
                        {
                            sb.AppendLine("\tconst enum " + Utility.CamelCaseClassName(io.Name) + " {");

                            foreach (IntellisenseProperty p in io.Properties)
                            {
                                WriteTypeScriptComment(p, sb);

                                if (p.InitExpression != null)
                                {
                                    sb.AppendLine("\t\t" + Utility.CamelCaseEnumValue(p.Name) + " = " + CleanEnumInitValue(p.InitExpression) + ",");
                                }
                                else
                                {
                                    sb.AppendLine("\t\t" + Utility.CamelCaseEnumValue(p.Name) + ",");
                                }
                            }

                            sb.AppendLine("\t}");
                        }
                        else
                        {
                            IEnumerable<string> propsNames = io.Properties.Select(p => "'" + Utility.CamelCaseEnumValue(p.Name) + "'");
                            var propsString = string.Join(" | ", propsNames);

                            sb.AppendLine("\ttype " + Utility.CamelCaseClassName(io.Name) + " = " + propsString + ";");
                        }
                    }
                    else
                    {
                        var type = Options.ClassInsteadOfInterface ? "\tclass " : "\tinterface ";
                        sb.Append($"export {type}").Append(Utility.CamelCaseClassName(io.Name)).Append(" ");

                        if (!string.IsNullOrEmpty(io.BaseName))
                        {
                            sb.Append("extends ");

                            if (!string.IsNullOrEmpty(io.BaseNamespace) && io.BaseNamespace != io.Namespace)
                            {
                                sb.Append(io.BaseNamespace).Append(".");
                            }

                            sb.Append(Utility.CamelCaseClassName(io.BaseName)).Append(" ");
                        }

                        WriteTSInterfaceDefinition(sb, "\t", io.Properties);
                        sb.AppendLine();
                    }
                }

                if (!Options.GlobalScope)
                {
                    sb.AppendLine("}");
                }
            }

            return sb.ToString();
        }

        private static string CleanEnumInitValue(string value)
        {
            value = value.TrimEnd('u', 'U', 'l', 'L'); //uint ulong long
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            var trimedValue = value.TrimStart('0'); // prevent numbers to be parsed as octal in js.
            if (trimedValue.Length > 0)
            {
                return trimedValue;
            }

            return "0";
        }


        private static void WriteTypeScriptComment(IntellisenseProperty p, StringBuilder sb)
        {
            if (string.IsNullOrEmpty(p.Summary))
            {
                return;
            }

            sb.AppendLine("\t\t/** " + _whitespaceTrimmer.Replace(p.Summary, "") + " */");
        }

        private static void WriteTSInterfaceDefinition(StringBuilder sb, string prefix,
            IEnumerable<IntellisenseProperty> props)
        {
            sb.AppendLine("{");

            foreach (IntellisenseProperty p in props)
            {
                WriteTypeScriptComment(p, sb);
                sb.AppendFormat("{0}\t{1}: ", prefix, Utility.CamelCasePropertyName(p.NameWithOption));

                if (p.Type.IsKnownType)
                {
                    sb.Append(p.Type.TypeScriptName);
                }
                else
                {
                    if (p.Type.Shape == null)
                    {
                        sb.Append("any");
                    }
                    else
                    {
                        WriteTSInterfaceDefinition(sb, prefix + "\t", p.Type.Shape);
                    }
                }
                if (p.Type.IsArray)
                {
                    sb.Append("[]");
                }

                sb.AppendLine(";");
            }

            sb.Append(prefix).Append("}");
        }
    }
}
