using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HandlebarsDotNet;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace CsToTs.TypeScript {

    public static class Helper {
        private static readonly BindingFlags BindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static;
        private static readonly Lazy<string> _lazyTemplate = new Lazy<string>(GetDefaultTemplate);
        private static string Template => _lazyTemplate.Value;

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

        private static TypeDefinition PopulateTypeDefinition(Type type, TypeScriptContext context) {
            if (type == null) return null;
            if (type.IsGenericParameter) return null;
            var typeCode = Type.GetTypeCode(type);
            if (typeCode != TypeCode.Object) return null;
            if (SkipCheck(type.ToString(), context.Options)) return null;

            if (type.IsConstructedGenericType) {
                type.GetGenericArguments().ToList().ForEach(t => PopulateTypeDefinition(t, context));
                type = type.GetGenericTypeDefinition();
            }

            var existing = context.Types.FirstOrDefault(t => t.ClrType == type);
            if (existing != null) return existing;

            var interfaceRefs = GetInterfaces(type, context);

            var useInterface = context.Options.UseInterfaceForClasses; 
            var isInterface = type.IsInterface || (useInterface != null && useInterface(type));
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
            typeDef.Ctors.AddRange(GetCtors(type, context));


            if (isInterface) {
                var staticMembers = GetMembers(type, context, true);
                var staticMethods = GetMethods(type, context, true);
                if (staticMembers.ToArray().Length > 0 || staticMethods.ToArray().Length > 0) {
                    var constructorDeclaration = $"interface {typeName}Constructor";
                    var instanceDeclaration = $"declare const {typeName}: {typeName}Constructor;";
                    var constructorDef = new TypeDefinition(type, typeName + "Constructor", constructorDeclaration, skipDeclaration, instanceDeclaration);
                    context.Types.Add(constructorDef);
                    constructorDef.Members.AddRange(staticMembers);
                    constructorDef.Methods.AddRange(staticMethods);
                }   
            }

            return typeDef;
        }

        private static List<CtorDefinition> GetCtors(Type type, TypeScriptContext context) {
            return type.GetConstructors().ToList().Select((ctorInfo) => {
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
                string declaration;
                if (method.IsGenericMethod) {
                    var methodName = memberRenamer(method);
                    
                    var genericPrms = method.GetGenericArguments().Select(t => GetTypeRef(t, context));
                    declaration = $"{methodName}<{string.Join(", ", genericPrms)}>";
                }
                else {
                    declaration = $"{memberRenamer(method)}";
                }

                var parameters = method.GetParameters()
                    .Select(p => new MemberDefinition(p.Name, GetTypeRef(p.ParameterType, context)));
                
                var decorators = useDecorators(method);

                var skipAttribute = method.GetCustomAttribute(typeof(HideFromTS), false);
                if (skipAttribute != null) {
                    continue;
                }
                
                var returnType = GetTypeRef(method.ReturnType, context);
                var methodDefinition = new MethodDefinition(declaration, parameters, null, decorators, returnType);

                if (shouldGenerateMethod(method, methodDefinition)) {
                    retVal.Add(methodDefinition);
                }
            }

            return retVal;
        }

        private static string GetTypeRef(Type type, TypeScriptContext context) {
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
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/gg.easy.airship/Runtime/Code/TSCodeGen/CsToTs/TypeScript/template.handlebars");
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