using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using HandlebarsDotNet;
using PlasticPipe.PlasticProtocol.Messages;
using SkbKontur.TypeScript.ContractGenerator.CodeDom;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace CsToTs.TypeScript {

    public static class Helper {
        private static readonly BindingFlags BindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static;
        private static readonly Lazy<string> _lazyTemplate = new Lazy<string>(GetDefaultTemplate);
        private static string Template => _lazyTemplate.Value;
        private static XmlDocument commentDoc;

        private static bool SkipCheck(string s, TypeScriptOptions o) =>
            s != null && o.SkipTypePatterns.Any(p => Regex.Match(s, p).Success);
        
        private static bool SkipClassDeclarationCheck(string s, TypeScriptOptions o) =>
            s != null && o.SkipClassDeclarationPatterns.Any(p => Regex.Match(s, p).Success);

        
        internal static string GenerateTypeScript(IEnumerable<Type> types, TypeScriptOptions options) {
            var context = new TypeScriptContext(options);
            GetTypeScriptDefinitions(types, context);

            Handlebars.Configuration.TextEncoder = new HtmlEncoder();

            var generator = Handlebars.Compile(Template);
            return generator(context);
        }

        private static void GetTypeScriptDefinitions(IEnumerable<Type> types, TypeScriptContext context) {
            foreach (var type in types) {
                if (!type.IsEnum) {
                    PopulateTypeDefinition(type, context);
                }
                else {
                    PopulateEnumDefinition(type, context);
                }
            }
        }

        private static Type UnwrapTaskType(Type type, out bool isTask)
        {
            isTask = false;

            if (type == typeof(Task))
            {
                isTask = true;
                return typeof(void);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                isTask = true;
                var resPropInfo = type.GetProperty("Result")!;
                return resPropInfo.PropertyType;
            }

            return type;
        }

        private static TypeDefinition PopulateTypeDefinition(Type type, TypeScriptContext context) {
            if (type == null) return null;

            if (type.IsGenericParameter) return null;
            var typeCode = Type.GetTypeCode(type);
            if (typeCode != TypeCode.Object) return null;
            if (SkipCheck(type.ToString(), context.Options)) return null;

            if (type.IsConstructedGenericType) {
                if (type.Name != "Singleton`1") {
                    return null;
                }
                type.GetGenericArguments().ToList().ForEach(t => {
                    if (t.Name == type.Name) return;
                    PopulateTypeDefinition(t, context);
                });
                type = type.GetGenericTypeDefinition();
            }

            var existing = context.Types.FirstOrDefault(t => t.ClrType == type);
            if (existing != null) return existing;

            var interfaceRefs = GetInterfaces(type, context);

            var useInterface = context.Options.UseInterfaceForClasses; 
            var isInterface = type.IsStruct() || type.IsInterface || (useInterface != null && useInterface(type));
            var baseTypeRef = string.Empty;
            if (type.IsClass) {
                if (type.BaseType != typeof(object) && PopulateTypeDefinition(type.BaseType, context) != null) {
                    baseTypeRef = GetTypeRef(type.BaseType, context);
                }
                else if (context.Options.DefaultBaseType != null) {
                    baseTypeRef = context.Options.DefaultBaseType(type);
                }
            }

            string declaration, typeName;
            if (!type.IsGenericType) {
                typeName = declaration = ApplyRename(type.Name, context);
            }
            else {
                var genericPrms = type.GetGenericArguments().Select(g => {
                    var constraints = g.GetGenericParameterConstraints()
                        .Where(c => PopulateTypeDefinition(c, context) != null)
                        .Select(c => GetTypeRef(c, context))
                        .ToList();

                    if (constraints.Any()) {
                        return $"{g.Name} extends {string.Join(" & ", constraints)}";
                    }

                    return g.Name;
                });
                
                typeName = ApplyRename(StripGenericFromName(type.Name), context);
                var genericPrmStr = string.Join(", ", genericPrms);
                declaration = $"{typeName}<{genericPrmStr}>";
            }
            
            if (isInterface) {
                declaration = $"interface {declaration}";

                if (!string.IsNullOrEmpty(baseTypeRef)) {
                    interfaceRefs.Insert(0, baseTypeRef);
                }
            }
            else {
                var abs = type.IsAbstract ? " abstract" : string.Empty;
                declaration = $"export{abs} class {declaration}";

                if (!string.IsNullOrEmpty(baseTypeRef)) {
                    declaration = $"{declaration} extends {baseTypeRef}";
                }
            }
            
            if (interfaceRefs.Any()) {
                var imp = isInterface ? "extends" : "implements";
                var interfaceRefStr = string.Join(", ", interfaceRefs);
                declaration = $"{declaration} {imp} {interfaceRefStr}";
            }

            if (typeName.Contains("&")) {
                return null;
            }
            
            var skipDeclaration = SkipClassDeclarationCheck(type.ToString(), context.Options);
            if (skipDeclaration) {
                Debug.Log("skipping: " + type.ToString());
            }
            
            var typeDef = new TypeDefinition(type, typeName, declaration, skipDeclaration, null);
            context.Types.Add(typeDef); 
            typeDef.Members.AddRange(GetMembers(type, context));
            typeDef.Methods.AddRange(GetMethods(type, context));

            if (isInterface) {
                var staticMembers = GetMembers(type, context, true);
                var staticMethods = GetMethods(type, context, true);
                var ctors = GetCtors(type, context);
                if (ctors.Count > 0 || staticMembers.ToArray().Length > 0 || staticMethods.ToArray().Length > 0) {
                    var constructorDeclaration = $"interface {typeName}Constructor";
                    var instanceDeclaration = $"declare const {typeName}: {typeName}Constructor;";
                    var constructorDef = new TypeDefinition(type, typeName + "Constructor", constructorDeclaration, skipDeclaration, instanceDeclaration);
                    context.Types.Add(constructorDef);
                    constructorDef.Members.AddRange(staticMembers);
                    constructorDef.StaticMethods.AddRange(staticMethods);
                    constructorDef.Ctors.AddRange(ctors);
                }   
            }

            return typeDef;
        }

        private static List<CtorDefinition> GetCtors(Type type, TypeScriptContext context) {
            var result = type.GetConstructors().ToList().Select((ctorInfo) => {
                var parameters = ctorInfo.GetParameters()
                    .Select(p => new MemberDefinition(p.Name, GetTypeRef(p.ParameterType, context)));

                var returnType = ApplyRename(type.Name, context);
                if (type.IsGenericType) {
                    returnType = ApplyRename(StripGenericFromName(type.Name), context);
                    var genericPrms = type.GetGenericArguments().Select(t => GetTypeRef(t, context));
                    returnType = $"{returnType}<{string.Join(", ", genericPrms)}>";
                }
                var ctor = new CtorDefinition(null, parameters, returnType);
                return ctor;
            }).ToList();
            return result;
        }

        private static List<string> GetInterfaces(Type type, TypeScriptContext context) {
            var interfaces = type.GetInterfaces().ToList();
            return interfaces
                .Except(type.BaseType?.GetInterfaces() ?? Enumerable.Empty<Type>())
                .Except(interfaces.SelectMany(i => i.GetInterfaces())) // get only implemented by this type
                .Where(i => PopulateTypeDefinition(i, context) != null)
                .Select(i => GetTypeRef(i, context))
                .ToList();
        }

        private static EnumDefinition PopulateEnumDefinition(Type type, TypeScriptContext context) {
            var existing = context.Enums.FirstOrDefault(t => t.ClrType == type);
            if (existing != null) return existing;

            var members = Enum.GetNames(type)
                .Select(n => new EnumField(n, context.Options.UseStringsForEnums ? $"\"{n}\"" : Convert.ToInt32(Enum.Parse(type, n)).ToString()));

            var skipDeclaration = SkipClassDeclarationCheck(type.ToString(), context.Options);
            if (skipDeclaration) {
                Debug.Log("skipping: " + type.ToString());
            }
            
            var def = new EnumDefinition(type, ApplyRename(type.Name, context), members, skipDeclaration);
            context.Enums.Add(def);
            
            return def;
        }
        
        private static IEnumerable<MemberDefinition> GetMembers(Type type, TypeScriptContext context, bool staticOnly = false) {
            var memberRenamer = context.Options.MemberRenamer ?? new Func<MemberInfo,string>(x => x.Name);
            var useDecorators = context.Options.UseDecorators ?? new Func<MemberInfo, IEnumerable<string>>(_=> (new List<string>()));

            var fields = type.GetFields(BindingFlags);
            var memberDefs = fields
                .Where((a) => staticOnly ? a.IsStatic : !a.IsStatic)
                .Select(f => {
                    var fieldType = f.FieldType;
                    var nullable = false;
                    if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        // choose the generic parameter, rather than the nullable
                        fieldType = fieldType.GetGenericArguments()[0];
                        nullable = true;
                    }

                    return new MemberDefinition(memberRenamer(f), GetTypeRef(fieldType, context), nullable, useDecorators(f).ToList(), f.IsStatic);
                })
                .ToList();

            var nameSet = new HashSet<string>();
            var props = type.GetProperties(BindingFlags);
            props = props.Where((prop) => {
                if (nameSet.Contains(prop.Name)) {
                    return false;
                }

                nameSet.Add(prop.Name);
                return true;
            })
                .Where((a) => staticOnly ? a.IsStatic() : !a.IsStatic())
                .ToArray();
            memberDefs.AddRange(props
                .Select(p => {
                    var propertyType = p.PropertyType;
                    var nullable = false;
                    if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        // choose the generic parameter, rather than the nullable
                        propertyType = propertyType.GetGenericArguments()[0];
                        nullable = true;
                    }
                    return new MemberDefinition(memberRenamer(p), GetTypeRef(propertyType, context), nullable, useDecorators(p).ToList(), p.IsStatic());
                    })
                );

            return memberDefs;
        }

        private static IEnumerable<MethodDefinition> GetMethods(Type type, TypeScriptContext context, bool staticOnly = false) {
            var shouldGenerateMethod = context.Options.ShouldGenerateMethod;
            if (shouldGenerateMethod == null) return Enumerable.Empty<MethodDefinition>();

            var useDecorators = context.Options.UseDecorators ?? (_ => new List<string>());
            var memberRenamer = context.Options.MemberRenamer ?? (x => x.Name);

            var retVal = new List<MethodDefinition>();
            var methods = type.GetMethods(BindingFlags).Where(m => !m.IsSpecialName)
                .ToArray()
                .Where((a) => staticOnly ? a.IsStatic : !a.IsStatic)
                .OrderBy((a) => a.Name);

            foreach (var method in methods) {
                string declaration = memberRenamer(method);
                var comment = GetFunctionComment(method);
                string generics = "";
                if (method.IsGenericMethod) {
                    var genericPrms = method.GetGenericArguments().Select(t => GetTypeRef(t, context));
                    generics = $"<{string.Join(", ", genericPrms)}>";
                }

                var parameters = method.GetParameters()
                    .Select(p => new MemberDefinition(p.Name, GetTypeRef(p.ParameterType, context)));
                
                var decorators = useDecorators(method);

                var skipAttribute = method.GetCustomAttribute(typeof(HideFromTS), false);
                if (skipAttribute != null) {
                    continue;
                }
                
                var returnType = GetTypeRef(method.ReturnType, context);
                var methodDefinition = new MethodDefinition(declaration, generics, parameters, null, decorators, returnType, method.IsStatic, comment);

                if (shouldGenerateMethod(method, methodDefinition)) {
                    retVal.Add(methodDefinition);
                }
            }

            return retVal;
        }

        private static Dictionary<string, string> commentCache = new Dictionary<string, string>();
        private static void LoadXmlDocumentation() {
            // Load the UnityEngine XML documentation file
            #if UNITY_EDITOR_WIN
            string localXMLPath = "Editor\\Data\\Managed\\UnityEngine.xml";
            #else
            string localXMLPath = "Unity.app/Contents/Managed/UnityEngine.xml";
            #endif

            var editorPath = EditorApplication.applicationPath;
            //Strip away the actual editor file to go up a folder (Editor/Unity.exe)
            editorPath = editorPath.Remove(editorPath.Length-16);
            var xmlFile = editorPath + localXMLPath;
            //PC Goal: C:\Program Files\Unity\Hub\Editor\2023.2.3f1\Editor\Data\Managed\UnityEngine.xml
            //Mac Goal: /Applications/Unity/Hub/Editor/2023.2.3f1/Unity.app/Contents/Managed/UnityEngine.xml

            if (System.IO.File.Exists(xmlFile)) {
                using (XmlReader reader = XmlReader.Create(xmlFile))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "member")
                        {
                            string memberName = reader.GetAttribute("name");
                            string comment = string.Empty;
    
                            if (reader.ReadToDescendant("summary") && reader.ReadToDescendant("para")) {
                                comment = reader.ReadInnerXml().Trim();
                            }

                            if (comment != string.Empty) {
                                commentCache[memberName] = comment;
                            }
                        }
                    }
                }
            }else{
                Debug.LogWarning("Unable to find Editor XML at: " + xmlFile);
            }
        }
        
        private static string GetFunctionComment(MethodInfo methodInfo) {
            // Create commentCache if doesn't exist
            if (commentCache.Count == 0) {
                LoadXmlDocumentation();
            }
            
            // Find the member element for the method
            var parameterStr = methodInfo.GetParameters().Length == 0 ? "" : $"({string.Join(",", methodInfo.GetParameters().Select(info => info.ParameterType.FullName))})";
            string memberName = $"M:{methodInfo.DeclaringType.FullName}.{methodInfo.Name}{parameterStr}";
            if (commentCache.TryGetValue(memberName, out string comment))
            {
                return comment;
            }
            return "";
        }
        
        private static string GetTypeRef(Type type, TypeScriptContext context)
        {
            type = UnwrapTaskType(type, out var isTask);

            if (type.FullName == "System.Void") {
                return "void";
            }
            
            if (type.IsGenericParameter)
                return ApplyRename(type.Name, context);

            if (type.IsEnum) {
                var enumDef = PopulateEnumDefinition(type, context);
                return enumDef != null ? enumDef.Name : "unknown";
            }

            var typeCode = Type.GetTypeCode(type);
            if (typeCode != TypeCode.Object) 
                return GetPrimitiveMemberType(typeCode, context.Options);

            Type dictionaryType;
            if (IsClosedDictionaryType(type))
                dictionaryType = type;
            else
                dictionaryType = type.GetInterfaces().FirstOrDefault(IsClosedDictionaryType);

            if (dictionaryType != null)
            {
                var keyType = dictionaryType.GetGenericArguments().ElementAt(0);
                var valueType = dictionaryType.GetGenericArguments().ElementAt(1);
                return $"CSDictionary<{GetTypeRef(keyType, context)}, {GetTypeRef(valueType, context)}>";
            }

            Type enumerable;
            if (IsClosedEnumerableType(type))
                enumerable = type;
            else 
                enumerable = type.GetInterfaces().FirstOrDefault(IsClosedEnumerableType);

            if (enumerable != null)
                return $"CSArray<{GetTypeRef(enumerable.GetGenericArguments().First(), context)}>";
                
            var typeDef = PopulateTypeDefinition(type, context);
            if (typeDef == null) 
                return "unknown";

            var typeName = typeDef.Name;
            if (type.IsGenericType) {
                var genericPrms = type.GetGenericArguments().Select(t => GetTypeRef(t, context));
                return $"{typeName}<{string.Join(", ", genericPrms)}>";
            }

            return typeName;
        }
        
        private static string GetPrimitiveMemberType(TypeCode typeCode, TypeScriptOptions options) {
            switch (typeCode) {
                case TypeCode.Boolean:
                    return "boolean";
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return "number";
                case TypeCode.Char:
                case TypeCode.String:
                    return "string";
                case TypeCode.DateTime:
                    return options.UseDateForDateTime ? "Date" : "string";
                default:
                    return "unknown";
            }
        }

        private static string StripGenericFromName(string name) {
            name = name.Substring(0, name.IndexOf("`"));

            return name;
        }

        public static string ApplyRename(string typeName, TypeScriptContext context) {
            var options = context.Options;
            typeName = options.TypeRenamer != null ? options.TypeRenamer(typeName) : typeName;

            var checkName = typeName;
            // var i = 1;
            // while (context.Types.Any(td => td.Name == checkName)) {
            //     checkName = $"{typeName}{i++}";
            // }
            
            return checkName;
        }
 
        private static string GetDefaultTemplate() {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/gg.easy.airship/Runtime/Code/TSCodeGen/Editor/CsToTs/TypeScript/template.handlebars");
            return textAsset.text;

            // var ass = typeof(Generator).Assembly;
            // var resourceName = ass.GetManifestResourceNames().First(r => r.Contains("template.handlebars"));
            // using (var reader = new StreamReader(ass.GetManifestResourceStream(resourceName), Encoding.UTF8)) {
            //     return reader.ReadToEnd();
            // }
        }

        private static bool IsClosedEnumerableType(Type type) =>
            type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>);

        private static bool IsClosedDictionaryType(Type type) =>
            type.IsConstructedGenericType && (type.GetGenericTypeDefinition() == typeof(IDictionary<,>) || type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
    }
}