using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Code.Luau;
using HandlebarsDotNet;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace CsToTs.TypeScript {

    public static class Helper {
        private static readonly BindingFlags BindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static;
        private static readonly Lazy<string> _lazyTemplate = new Lazy<string>(GetDefaultTemplate);
        private static string Template => _lazyTemplate.Value;
        private static XmlDocument commentDoc;
        private static HashSet<Type> populateInProgress = new HashSet<Type>();

        private static bool SkipCheck(string s, TypeScriptOptions o) =>
            s != null && o.SkipTypePatterns.Any(p => Regex.Match(s, p).Success);
        
        private static bool SkipClassDeclarationCheck(string s, TypeScriptOptions o) =>
            s != null && o.SkipClassDeclarationPatterns.Any(p => Regex.Match(s, p).Success);

        
        internal static string GenerateTypeScript(IEnumerable<Type> types, TypeScriptOptions options) {
            var context = new TypeScriptContext(options);
            GetTypeScriptDefinitions(types, context);

            // I don't think we need this... It causes you've -> you&#x27ve;
            // Handlebars.Configuration.TextEncoder = new HtmlEncoder();

            var generator = Handlebars.Compile(Template);
            return generator(context);
        }

        private static void GetTypeScriptDefinitions(IEnumerable<Type> types, TypeScriptContext context) {
            populateInProgress.Clear();
            
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
                // Not sure what this was for... but it was causing issues (where generics wouldn't be added properly)
                // if (type.Name != "Singleton`1") {
                //     return null;
                // }
                type.GetGenericArguments().ToList().ForEach(t => {
                    if (t.Name == type.Name) return;
                    if (t.Name == "InternalCameraScreenshotRecorder") return;
                    PopulateTypeDefinition(t, context);
                });
                type = type.GetGenericTypeDefinition();
            }

            var existing = context.Types.FirstOrDefault(t => t.ClrType == type);
            if (existing != null) return existing;
            
            if (populateInProgress.Contains(type)) return null;
            populateInProgress.Add(type);

            var interfaceRefs = GetInterfaces(type, context);

            var useInterface = context.Options.UseInterfaceForClasses; 
            var isInterface = type.IsStruct() || type.IsInterface || (useInterface != null && useInterface(type));
            var baseTypeRef = string.Empty;
            // Delayed population to prevent infinite looping!
            var requiresPopulation = new List<Type>();
            if (type.IsClass) {
                if (type.BaseType != typeof(object) && type.BaseType != null) {
                    requiresPopulation.Add(type.BaseType);
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
            typeDef.Events.AddRange(GetEvents(type, context));

            if (isInterface) {
                var staticMembers = GetMembers(type, context, true);
                var staticMethods = GetMethods(type, context, true);
                var staticEvents = GetEvents(type, context, true);
                var ctors = GetCtors(type, context);
                if (ctors.Count > 0 || staticMembers.ToArray().Length > 0 || staticMethods.ToArray().Length > 0 || staticEvents.ToArray().Length > 0) {
                    var constructorDeclaration = $"interface {typeName}Constructor";
                    var instanceDeclaration = $"declare const {typeName}: {typeName}Constructor;";
                    var constructorDef = new TypeDefinition(type, typeName + "Constructor", constructorDeclaration, skipDeclaration, instanceDeclaration);
                    context.Types.Add(constructorDef);
                    constructorDef.Members.AddRange(staticMembers);
                    constructorDef.StaticMethods.AddRange(staticMethods);
                    constructorDef.StaticEvents.AddRange(staticEvents);
                    constructorDef.Ctors.AddRange(ctors);
                }   
            }

            // This should happen after we've pushed this typeDef to context to make sure we don't inf loop!
            foreach (var toPopulate in requiresPopulation) {
                PopulateTypeDefinition(toPopulate, context);
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
                .Where((a) => Attribute.GetCustomAttribute(a, typeof(ObsoleteAttribute)) == null)
                .Where((a) => !a.FieldType.IsSubclassOf(typeof(UnityEventBase)))
                .Select(f => {
                    var fieldType = f.FieldType;
                    var nullable = false;
                    if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        // choose the generic parameter, rather than the nullable
                        fieldType = fieldType.GetGenericArguments()[0];
                        nullable = true;
                    }

                    var comment = GetParameterComment(f.DeclaringType.FullName, f.Name);
                    var isReadonly = f.IsInitOnly;
                    return new MemberDefinition(memberRenamer(f), GetTypeRef(fieldType, context), nullable, useDecorators(f).ToList(), f.IsStatic, isReadonly, comment);
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
                .Where((a) => Attribute.GetCustomAttribute(a, typeof(ObsoleteAttribute)) == null)
                .Where((a) => !a.PropertyType.IsSubclassOf(typeof(UnityEventBase)))
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
                    var comment = GetParameterComment(p.DeclaringType.FullName, p.Name);
                    var isReadonly = p.GetSetMethod(false) == null;
                    return new MemberDefinition(memberRenamer(p), GetTypeRef(propertyType, context), nullable, useDecorators(p).ToList(), p.IsStatic(), isReadonly, comment);
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
                .Where((a) => Attribute.GetCustomAttribute(a, typeof(ObsoleteAttribute)) == null)
                .OrderBy((a) => a.Name);

            foreach (var method in methods) {
                string declaration = memberRenamer(method);
                var commentLines = GetFunctionComment(method);
                string generics = "";
                if (method.IsGenericMethod) {
                    var genericPrms = method.GetGenericArguments().Select(t => {
                        var baseStr = GetTypeRef(t, context);
                        var constraints = t.GetGenericParameterConstraints().Where(c => c != typeof(ValueType)).ToArray();
                        if (constraints.Length > 0) {
                            baseStr = $"{baseStr} extends {string.Join(" & ", constraints.Select((constraintType) => GetTypeRef(constraintType, context)))}";
                        }
                        return baseStr;
                    });
                    generics = $"<{string.Join(", ", genericPrms)}>";
                }

                var paramInfos = method.GetParameters();
                if (paramInfos.Any(p => p.IsOut)) continue; // Skip over any out variable methods
                
                var parameters = paramInfos
                    .Select(p => new MemberDefinition(p.Name, GetTypeRef(p.ParameterType, context)));
                
                var decorators = useDecorators(method);

                var skipAttribute = method.GetCustomAttribute(typeof(HideFromTS), false);
                if (skipAttribute != null) {
                    continue;
                }

                // Remove the context parameter from attached context methods
                if (method.GetCustomAttribute<AttachContext>() != null) {
                    parameters = parameters.Skip(1);
                }
                
                var returnType = GetTypeRef(method.ReturnType, context);
                var methodDefinition = new MethodDefinition(declaration, generics, parameters, null, decorators, returnType, method.IsStatic, commentLines);

                if (shouldGenerateMethod(method, methodDefinition)) {
                    retVal.Add(methodDefinition);
                }
            }

            return retVal;
        }

        private static IEnumerable<EventDefinition> GetEvents(Type type, TypeScriptContext context, bool staticOnly = false) {
            var useDecorators = context.Options.UseDecorators ?? (_ => new List<string>());
            var memberRenamer = context.Options.MemberRenamer ?? (x => x.Name);
            
            var events = type.GetEvents(BindingFlags);

            var eventDefs = events
                .Where((a) => staticOnly ? a.GetAddMethod().IsStatic : !a.GetAddMethod().IsStatic)
                .Where((a) => Attribute.GetCustomAttribute(a, typeof(ObsoleteAttribute)) == null)
                .Select(e => {
                    var eventHandlerType = e.EventHandlerType;
                    var nullable = false;
                    if (eventHandlerType.IsGenericType &&
                        eventHandlerType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                        // choose the generic parameter, rather than the nullable
                        eventHandlerType = eventHandlerType.GetGenericArguments()[0];
                        nullable = true;
                    }

                    var generics = "<void>";
                    if (eventHandlerType.IsGenericType) {
                        var genericPrms = eventHandlerType.GetGenericArguments().Select(t => GetTypeRef(t, context));
                        generics = $"<{string.Join(", ", genericPrms)}>";
                    }

                    var comment = GetParameterComment(e.DeclaringType.FullName, e.Name);
                    return new EventDefinition(memberRenamer(e), generics, nullable, useDecorators(e).ToList(), e.GetAddMethod().IsStatic, comment);
                });
            
            var nameSet = new HashSet<string>();
            var unityEventDefs = type.GetProperties(BindingFlags).Where((prop) => nameSet.Add(prop.Name))
                .Where((a) => a.PropertyType.IsSubclassOf(typeof(UnityEventBase)))
                .Where((a) => staticOnly ? a.IsStatic() : !a.IsStatic())
                .Where((a) => Attribute.GetCustomAttribute(a, typeof(ObsoleteAttribute)) == null)
                .Select(p => {
                    var eventHandlerType = p.PropertyType;
                    var nullable = false;
                    if (eventHandlerType.IsGenericType &&
                        eventHandlerType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                        // choose the generic parameter, rather than the nullable
                        eventHandlerType = eventHandlerType.GetGenericArguments()[0];
                        nullable = true;
                    }

                    var generics = "<void>";
                    if (eventHandlerType.IsGenericType) {
                        var genericPrms = eventHandlerType.GetGenericArguments().Select(t => GetTypeRef(t, context));
                        generics = $"<{string.Join(", ", genericPrms)}>";
                    }

                    var comment = GetParameterComment(p.DeclaringType.FullName, p.Name);
                    return new EventDefinition(memberRenamer(p), generics, nullable, useDecorators(p).ToList(), p.IsStatic(), comment);
                });

            return eventDefs.Concat(unityEventDefs).ToList();
        }

        private static Dictionary<string, string> commentCache = new Dictionary<string, string>();
        private static Dictionary<string, Dictionary<string, string>> commentParamCache = new Dictionary<string, Dictionary<string, string>>();
        private static bool grabbedCommentCache = false;
        private static void LoadXmlDocumentation() {
            grabbedCommentCache = true;
            
            // Load the UnityEngine XML documentation file
            #if UNITY_EDITOR_WIN
            string localXMLPath = "Data\\Managed\\UnityEngine.xml";
            #elif UNITY_EDITOR_LINUX
            string localXMLPath = "Data/Managed/UnityEngine.xml";
            #else
            string localXMLPath = "Unity.app/Contents/Managed/UnityEngine.xml";
            #endif

            var editorPath = EditorApplication.applicationPath;
            //Strip away the actual editor file to go up a folder (Editor/Unity.exe)
            editorPath += "/../";
            var xmlFile = editorPath + localXMLPath;
            //PC Goal: C:\Program Files\Unity\Hub\Editor\2023.2.3f1\Editor\Data\Managed\UnityEngine.xml
            //Mac Goal: /Applications/Unity/Hub/Editor/2023.2.3f1/Unity.app/Contents/Managed/UnityEngine.xml

            if (System.IO.File.Exists(xmlFile)) {
                using (XmlReader reader = XmlReader.Create(xmlFile)) {
                    while (reader.Read()) {
                        if (reader.NodeType != XmlNodeType.Element) continue;
                        
                        // Entry
                        if (reader.Name == "member") {
                            string memberName = reader.GetAttribute("name");
                            if (memberName == null) continue;
                            
                            using (XmlReader subtree = reader.ReadSubtree()) {
                                while (subtree.Read()) {
                                    if (subtree.Name == "summary") {
                                        string comment = string.Empty;
                                        if (subtree.ReadToDescendant("para")) {
                                            comment = reader.ReadString().Trim();
                                        }

                                        if (comment != string.Empty) {
                                            commentCache[memberName] = comment;
                                        }
                                    }

                                    // Grab @params
                                    if (subtree.Name == "param") {
                                        var paramName = subtree.GetAttribute("name");
                                        var paramDict = commentParamCache.GetValueOrDefault(memberName) ?? new Dictionary<string, string>();
                                        commentParamCache[memberName] = paramDict;

                                        paramDict[paramName] = subtree.ReadString().Trim();
                                    }
                                }
                            }
                        }
                    }
                }
            }else{
                Debug.LogWarning("Unable to find Editor XML at: " + xmlFile);
            }
        }
        
        /// <summary>
        /// Returns comment lines as a list
        /// </summary>
        private static List<string> GetParameterComment(string fullDeclaringType, string name) {
            // Create commentCache if doesn't exist
            if (!grabbedCommentCache) {
                LoadXmlDocumentation();
            }
            
            List<string> commentLines = new List<string>();
            string memberName = $"P:{fullDeclaringType}.{name}";
            if (commentCache.TryGetValue(memberName, out string comment)) {
                commentLines.Add(comment);
                
                // Add Unity docs link
                if (fullDeclaringType.StartsWith("UnityEngine")) {
                    var unityEngineType = fullDeclaringType.Substring("UnityEngine".Length);
                    if (unityEngineType.StartsWith(".")) unityEngineType = unityEngineType.Substring(1); 
                    commentLines.Add("");
                    commentLines.Add($"More info: {{@link https://docs.unity3d.com/ScriptReference/{unityEngineType}-{name}.html | {unityEngineType}.{name}}}");
                }
            }
            return commentLines;
        }
        
        /// <summary>
        /// Returns comment lines as a list
        /// </summary>
        private static List<string> GetFunctionComment(MethodInfo methodInfo) {
            // Create commentCache if doesn't exist
            if (!grabbedCommentCache) {
                LoadXmlDocumentation();
            }
            
            // Find the member element for the method
            List<string> parameterStrs = new List<string>();
            var methodParams = methodInfo.GetParameters();
            var parameterStr = $"({string.Join(",", methodParams.Select(info => info.ParameterType.FullName))})";
            parameterStrs.Add(parameterStr);
            if (methodParams.Length == 0) parameterStrs.Add("");

            List<string> commentLines = new List<string>();
            foreach (var str in parameterStrs) {
                string memberName = $"M:{methodInfo.DeclaringType.FullName}.{methodInfo.Name}{str}";
                if (commentCache.TryGetValue(memberName, out string comment)) {
                    commentLines.Add(comment);
                    
                    // Grab params
                    if (commentParamCache.TryGetValue(memberName, out Dictionary<string, string> commentParams)) {
                        foreach (var param in methodParams) {
                            if (commentParams.TryGetValue(param.Name, out var paramComment)) {
                                if (paramComment.Length == 0) continue;
                                
                                commentLines.Add($"@param {param.Name} {paramComment}");
                            }
                        }
                    }
                    
                    // Add Unity docs link
                    if (methodInfo.DeclaringType.Namespace.StartsWith("UnityEngine")) {
                        var unityEngineType = methodInfo.DeclaringType.FullName.Substring("UnityEngine".Length);
                        if (unityEngineType.StartsWith(".")) unityEngineType = unityEngineType.Substring(1); 
                        commentLines.Add("");
                        commentLines.Add($"More info: {{@link https://docs.unity3d.com/ScriptReference/{unityEngineType}.{methodInfo.Name}.html | {unityEngineType}.{methodInfo.Name}}}");
                    }
                    break;
                }
            }
            return commentLines;
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
                return $"Readonly<{GetTypeRef(enumerable.GetGenericArguments().First(), context)}[]>";

            // This is very specifically to resolve Debug.Log taking in an Object
            if (type == typeof(object)) return "unknown";
            
            var typeDef = PopulateTypeDefinition(type, context); // Populate
            var typeName = ApplyRename(StripGenericFromName(type.Name), context);
            if (type.IsGenericType) {
                var genericPrms = type.GetGenericArguments().Select(t => GetTypeRef(t, context));
                return $"{typeName}<{string.Join(", ", genericPrms)}>";
            }

            // Clear off the ending "&" from type name. This exists for ref types (which we support)
            // and for out types (which we exclude anyway, so this doesn't matter for those).
            typeName = typeName.Replace("&", "");
            
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
            var idx = name.IndexOf("`", StringComparison.Ordinal);
            return idx == -1 ? name : name.Substring(0, idx);
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