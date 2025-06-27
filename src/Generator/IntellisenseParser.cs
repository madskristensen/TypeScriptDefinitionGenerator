using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EnvDTE;
using EnvDTE80;
using TypeScriptDefinitionGenerator.Helpers;
using CodeAttributeArgument = EnvDTE80.CodeAttributeArgument;
using CodeNamespace = EnvDTE.CodeNamespace;

namespace TypeScriptDefinitionGenerator
{
    public static class IntellisenseParser
    {
        private static readonly string DefaultModuleName = Options.DefaultModuleName;
        private const string ModuleNameAttributeName = "TypeScriptModule";
        private static readonly Regex IsNumber = new Regex("^[0-9a-fx]+[ul]{0,2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static Project _project;

        //internal static class Ext
        //{
        //    public const string TypeScript = ".d.ts";
        //}

        public static Dictionary<ProjectItem, List<IntellisenseObject>> ProcessFile(ProjectItem item)
        {
            var resultsByFile = new Dictionary<ProjectItem, List<IntellisenseObject>>();
            var processedTypes = new HashSet<string>(); // Keep track of full type names we've already processed

            ProcessProjectItem(item, resultsByFile, processedTypes);

            return resultsByFile;
        }

        private static void ProcessProjectItem(ProjectItem item, Dictionary<ProjectItem, List<IntellisenseObject>> resultsByFile, HashSet<string> processedTypes)
        {
            if (item?.FileCodeModel == null || item.ContainingProject == null) return;
            if (resultsByFile.ContainsKey(item)) return; // Already processed or in-progress

            string sourcePath = item.FileNames[1];
            string dtsPath = GenerationService.GenerateFileName(item);

            if (File.Exists(dtsPath))
            {
                string firstLine = File.ReadLines(dtsPath).FirstOrDefault();
                if (firstLine != null && firstLine.StartsWith("//#hash:"))
                {
                    string oldHash = firstLine.Substring(8).Trim();
                    string newHash = CalculateFileHash(sourcePath);

                    if (oldHash == newHash)
                    {
                        // The file is unchanged, we can skip it completely.
                        VSHelpers.WriteOnOutputWindow($"   -> Skipping '{item.Name}' (unchanged).");
                        return;
                    }
                }
            }

            var fileObjects = new List<IntellisenseObject>();
            resultsByFile[item] = fileObjects;
            _project = item.ContainingProject;

            foreach (CodeElement element in item.FileCodeModel.CodeElements)
            {
                ProcessCodeElement(element, item, resultsByFile, processedTypes);
            }
        }

        public static string CalculateFileHash(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return string.Empty;
            }

            byte[] fileBytes = File.ReadAllBytes(filePath);
            var hash = XxHash64.Hash(fileBytes);

            return Convert.ToBase64String(hash);
        }

        private static void ProcessCodeElement(CodeElement element, ProjectItem currentItem, Dictionary<ProjectItem, List<IntellisenseObject>> resultsByFile, HashSet<string> processedTypes)
        {
            if (element.Kind == vsCMElement.vsCMElementNamespace)
            {
                foreach (CodeElement member in ((CodeNamespace)element).Members)
                {
                    ProcessCodeElement(member, currentItem, resultsByFile, processedTypes);
                }
            }
            else if (element.Kind == vsCMElement.vsCMElementEnum)
            {
                ProcessEnum((CodeEnum)element, currentItem, resultsByFile, processedTypes);
            }
            else if (element.Kind == vsCMElement.vsCMElementClass)
            {
                ProcessClass((CodeClass)element, currentItem, resultsByFile, processedTypes);
            }
        }

        private static bool ShouldProcess(CodeElement member) => member.Kind == vsCMElement.vsCMElementClass || member.Kind == vsCMElement.vsCMElementEnum;

        private static void ProcessEnum(CodeEnum element, ProjectItem currentItem, Dictionary<ProjectItem, List<IntellisenseObject>> resultsByFile, HashSet<string> processedTypes)
        {
            if (processedTypes.Contains(element.FullName)) return;
            processedTypes.Add(element.FullName);

            var data = new IntellisenseObject
            {
                Name = element.Name,
                IsEnum = true,
                FullName = element.FullName,
                Namespace = GetNamespace(element),
                Summary = GetSummary(element)
            };

            foreach (CodeVariable codeEnum in element.Members.OfType<CodeVariable>())
            {
                data.Properties.Add(new IntellisenseProperty
                {
                    Name = codeEnum.Name,
                    Summary = GetSummary(codeEnum),
                    InitExpression = GetInitializer(codeEnum.InitExpression)
                });
            }

            if (data.Properties.Any())
            {
                resultsByFile[currentItem].Add(data); // Add to the list for the current file
            }
        }

        private static void ProcessClass(CodeClass cc, ProjectItem currentItem, Dictionary<ProjectItem, List<IntellisenseObject>> resultsByFile, HashSet<string> processedTypes)
        {
            if (processedTypes.Contains(cc.FullName)) return;
            processedTypes.Add(cc.FullName);

            // Recursively process nested types first
            foreach (CodeElement member in cc.Members)
            {
                if (ShouldProcess(member))
                {
                    ProcessCodeElement(member, currentItem, resultsByFile, processedTypes);
                }
            }

            CodeClass baseClass = cc.Bases.Cast<CodeElement>().OfType<CodeClass>().FirstOrDefault(c => c.Kind == vsCMElement.vsCMElementClass && c.FullName != "System.Object");

            // Recursively process the base class if it's in the project
            if (baseClass?.InfoLocation == vsCMInfoLocation.vsCMInfoLocationProject)
            {
                ProcessProjectItem(baseClass.ProjectItem, resultsByFile, processedTypes);
            }

            var ns = GetNamespace(cc);
            var className = GetClassName(cc);
            var references = new HashSet<string>();
            var properties = GetProperties(cc, cc.Members, new HashSet<string>(), references, resultsByFile, processedTypes).ToList();

            var intellisenseObject = new IntellisenseObject(properties, references)
            {
                Namespace = ns,
                Name = className,
                BaseNamespace = baseClass != null ? GetNamespace(baseClass) : null,
                BaseName = baseClass != null ? GetClassName(baseClass) : null,
                FullName = cc.FullName,
                Summary = GetSummary(cc)
            };

            resultsByFile[currentItem].Add(intellisenseObject);
        }

        private static IEnumerable<IntellisenseProperty> GetProperties(CodeClass rootClass, CodeElements props, HashSet<string> traversedTypes, HashSet<string> references, Dictionary<ProjectItem, List<IntellisenseObject>> resultsByFile, HashSet<string> processedTypes)
        {
            return from p in props.OfType<CodeProperty>()
                   where !p.Attributes.Cast<CodeAttribute>().Any(HasIgnoreAttribute)
                   where vsCMAccess.vsCMAccessPublic == p.Access && p.Getter != null && !p.Getter.IsShared && IsPublic(p.Getter)
                   select new IntellisenseProperty
                   {
                       Name = GetName(p),
                       Type = GetType(rootClass, p.Type, traversedTypes, references, resultsByFile, processedTypes),
                       Summary = GetSummary(p)
                   };
        }

        private static bool HasIgnoreAttribute(CodeAttribute attribute)
        {
            string fullName = attribute.FullName;
            return fullName == "System.Runtime.Serialization.IgnoreDataMemberAttribute" ||
                   fullName == "Newtonsoft.Json.JsonIgnoreAttribute" ||
                   fullName == "System.Web.Script.Serialization.ScriptIgnoreAttribute";
        }

        private static bool IsPublic(CodeFunction cf)
        {
            try
            {
                return cf.Access == vsCMAccess.vsCMAccessPublic;
            }
            catch (COMException)
            {
                if (cf.Parent is CodeProperty cp)
                {
                    return cp.Access == vsCMAccess.vsCMAccessPublic;
                }
            }
            return false;
        }

        private static string GetClassName(CodeClass cc) => GetDataContractName(cc, "Name") ?? cc.Name;
        private static string GetNamespace(CodeClass cc) => Options.UseCSharpNamespace ? cc.Namespace.FullName : (GetDataContractName(cc, "Namespace") ?? GetNamespace(cc.Attributes));
        private static string GetNamespace(CodeEnum ce) => Options.UseCSharpNamespace ? ce.Namespace.FullName : (GetDataContractName(ce, "Namespace") ?? GetNamespace(ce.Attributes));

        private static string GetDataContractName(CodeClass cc, string attrName)
        {
            IEnumerable<CodeAttribute> dataContractAttribute = cc.Attributes.Cast<CodeAttribute>().Where(a => a.Name == "DataContract");
            return GetDataContractNameInner(dataContractAttribute, attrName);
        }
        private static string GetDataContractName(CodeEnum cc, string attrName)
        {
            IEnumerable<CodeAttribute> dataContractAttribute = cc.Attributes.Cast<CodeAttribute>().Where(a => a.Name == "DataContract");
            return GetDataContractNameInner(dataContractAttribute, attrName);
        }
        private static string GetDataContractNameInner(IEnumerable<CodeAttribute> dataContractAttribute, string attrName)
        {
            if (!dataContractAttribute.Any()) return null;
            var keyValues = dataContractAttribute.First().Children.OfType<CodeAttributeArgument>()
                           .ToDictionary(a => a.Name, a => (a.Value ?? "").Trim('\"', '\''));
            return keyValues.TryGetValue(attrName, out var name) ? name : null;
        }
        private static string GetNamespace(CodeElements attrs)
        {
            if (attrs == null) return Options.DefaultModuleName;

            return (from a in attrs.Cast<CodeAttribute2>()
                    where a.Name.EndsWith("TypeScriptModule", StringComparison.OrdinalIgnoreCase)
                    from arg in a.Arguments.Cast<CodeAttributeArgument>()
                    let v = (arg.Value ?? "").Trim('\"')
                    where !string.IsNullOrWhiteSpace(v)
                    select v).FirstOrDefault() ?? Options.DefaultModuleName;
        }

        private static IntellisenseType GetType(CodeClass rootElement, CodeTypeRef codeTypeRef, HashSet<string> traversedTypes, HashSet<string> references, Dictionary<ProjectItem, List<IntellisenseObject>> resultsByFile, HashSet<string> processedTypes)
        {
            // ... (initial type deduction logic is the same)
            var isArray = codeTypeRef.TypeKind == vsCMTypeRef.vsCMTypeRefArray;
            var isCollection = !isArray && codeTypeRef.AsString.StartsWith("System.Collections", StringComparison.Ordinal);
            var isDictionary = false;

            CodeTypeRef effectiveTypeRef = codeTypeRef;
            if (isArray)
            {
                effectiveTypeRef = codeTypeRef.ElementType;
            }
            else if (isCollection)
            {
                effectiveTypeRef = TryToGuessGenericArgument(rootElement, codeTypeRef);
                isDictionary = effectiveTypeRef.AsString.Contains("KeyValuePair") || codeTypeRef.AsString.Contains("Dictionary");
            }

            var typeName = effectiveTypeRef.AsFullName;

            try
            {
                var codeClass = effectiveTypeRef.CodeType as CodeClass2;
                var codeEnum = effectiveTypeRef.CodeType as CodeEnum;
                var isPrimitive = IsPrimitive(effectiveTypeRef);

                // If it's a type from our project, ensure it gets processed.
                if (!isPrimitive && effectiveTypeRef.CodeType?.InfoLocation == vsCMInfoLocation.vsCMInfoLocationProject)
                {
                    // This is the key recursive call. It will populate the resultsByFile dictionary
                    // for the dependent type if it hasn't been processed yet.
                    ProcessProjectItem(effectiveTypeRef.CodeType.ProjectItem, resultsByFile, processedTypes);
                }

                var result = new IntellisenseType
                {
                    IsArray = !isDictionary && (isArray || isCollection),
                    IsDictionary = isDictionary,
                    CodeName = effectiveTypeRef.AsString,
                };

                if (codeClass != null)
                {
                    result.ClientSideReferenceName = GetNamespace(codeClass) + "." + Utility.CamelCaseClassName(GetClassName(codeClass));
                }
                else if (codeEnum != null)
                {
                    result.ClientSideReferenceName = GetNamespace(codeEnum) + "." + Utility.CamelCaseClassName(codeEnum.Name);
                }

                return result;
            }
            catch (Exception ex)
            {
                VSHelpers.WriteOnOutputWindow($"ERROR - Could not resolve type '{typeName}'. It will be treated as 'any'. Details: {ex.Message}");
                return new IntellisenseType { CodeName = "any" }; // Fallback to any
            }
        }

        private static CodeTypeRef TryToGuessGenericArgument(CodeClass rootElement, CodeTypeRef codeTypeRef)
        {
            var codeTypeRef2 = codeTypeRef as CodeTypeRef2;
            if (codeTypeRef2 == null || !codeTypeRef2.IsGeneric)
            {
                return codeTypeRef;
            }

            // There is no way to extract generic parameter as CodeTypeRef or something similar
            // (see http://social.msdn.microsoft.com/Forums/vstudio/en-US/09504bdc-2b81-405a-a2f7-158fb721ee90/envdte-envdte80-codetyperef2-and-generic-types?forum=vsx)
            // but we can make it work at least for some simple case with the following heuristic:
            //  1) get the argument's local name by parsing the type reference's full text
            //  2) if it's a known primitive (i.e. string, int, etc.), return that
            //  3) otherwise, guess that it's a type from the same namespace and same project,
            //     and use the project CodeModel to retrieve it by full name
            //  4) if CodeModel returns null - well, bad luck, don't have any more guesses
            var typeNameAsInCode = codeTypeRef2.AsString.Split('<', '>').ElementAtOrDefault(1) ?? "";
            CodeModel projCodeModel;

            try
            {
                projCodeModel = rootElement.ProjectItem.ContainingProject.CodeModel;
            }
            catch (COMException)
            {
                projCodeModel = _project.CodeModel;
            }

            CodeType codeType = projCodeModel.CodeTypeFromFullName(TryToGuessFullName(typeNameAsInCode));

            if (codeType != null)
            {
                return projCodeModel.CreateCodeTypeRef(codeType);
            }

            return codeTypeRef;
        }

        private static readonly Dictionary<string, Type> _knownPrimitiveTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
            { "string", typeof( string ) },
            { "int", typeof( int ) },
            { "long", typeof( long ) },
            { "short", typeof( short ) },
            { "byte", typeof( byte ) },
            { "uint", typeof( uint ) },
            { "ulong", typeof( ulong ) },
            { "ushort", typeof( ushort ) },
            { "sbyte", typeof( sbyte ) },
            { "float", typeof( float ) },
            { "double", typeof( double ) },
            { "decimal", typeof( decimal ) },
        };

        private static string TryToGuessFullName(string typeName)
        {
            if (_knownPrimitiveTypes.TryGetValue(typeName, out Type primitiveType))
            {
                return primitiveType.FullName;
            }

            return typeName;
        }

        private static bool IsPrimitive(CodeTypeRef codeTypeRef)
        {
            if (codeTypeRef.TypeKind != vsCMTypeRef.vsCMTypeRefOther && codeTypeRef.TypeKind != vsCMTypeRef.vsCMTypeRefCodeType)
            {
                return true;
            }

            if (codeTypeRef.AsString.EndsWith("DateTime", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        // Maps attribute name to array of attribute properties to get resultant name from
        private static readonly IReadOnlyDictionary<string, string[]> nameAttributes = new Dictionary<string, string[]>
        {
            { "DataMember", new [] { "Name" } },
            { "JsonProperty", new [] { "", "PropertyName" } }
        };

        private static string GetName(CodeProperty property)
        {
            foreach (CodeAttribute attr in property.Attributes)
            {
                var className = Path.GetExtension(attr.Name);

                if (string.IsNullOrEmpty(className))
                {
                    className = attr.Name;
                }

                if (!nameAttributes.TryGetValue(className, out var argumentNames))
                {
                    continue;
                }

                CodeAttributeArgument value = attr.Children.OfType<CodeAttributeArgument>().FirstOrDefault(a => argumentNames.Contains(a.Name));

                if (value == null)
                {
                    break;
                }

                // Strip the leading & trailing quotes
                return value.Value.Trim('@', '\'', '"');
            }

            return property.Name.Trim('@');
        }

        // External items throw an exception from the DocComment getter
        private static string GetSummary(CodeProperty property) { return property.InfoLocation != vsCMInfoLocation.vsCMInfoLocationProject ? null : GetSummary(property.InfoLocation, property.DocComment, property.Comment, property.FullName); }

        private static string GetSummary(CodeClass property) { return GetSummary(property.InfoLocation, property.DocComment, property.Comment, property.FullName); }

        private static string GetSummary(CodeEnum property) { return GetSummary(property.InfoLocation, property.DocComment, property.Comment, property.FullName); }

        private static string GetSummary(CodeVariable property) { return GetSummary(property.InfoLocation, property.DocComment, property.Comment, property.FullName); }

        private static string GetSummary(vsCMInfoLocation location, string xmlComment, string inlineComment, string fullName)
        {
            if (location != vsCMInfoLocation.vsCMInfoLocationProject || (string.IsNullOrWhiteSpace(xmlComment) && string.IsNullOrWhiteSpace(inlineComment)))
            {
                return null;
            }

            try
            {
                var summary = "";
                if (!string.IsNullOrWhiteSpace(xmlComment))
                {
                    summary = XElement.Parse(xmlComment)
                               .Descendants("summary")
                               .Select(x => x.Value)
                               .FirstOrDefault();
                }
                if (!string.IsNullOrEmpty(summary))
                {
                    return summary.Trim();
                }

                if (!string.IsNullOrWhiteSpace(inlineComment))
                {
                    return inlineComment.Trim();
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static string GetInitializer(object initExpression)
        {
            if (initExpression != null)
            {
                var initializer = initExpression.ToString();
                if (IsNumber.IsMatch(initializer))
                {
                    return initializer;
                }
            }
            return null;
        }
    }
}
