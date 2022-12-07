using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Configuration;
using Kiota.Builder.Extensions;
using SharpYaml.Serialization;

namespace Kiota.Builder.Refiners;
public class TypeScriptRefiner : CommonLanguageRefiner, ILanguageRefiner
{
    public TypeScriptRefiner(GenerationConfiguration configuration) : base(configuration) { }
    public override Task Refine(CodeNamespace generatedCode, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReplaceIndexersByMethodsWithParameter(generatedCode, generatedCode, false, "ById");
            RemoveCancellationParameter(generatedCode);
            CorrectCoreType(generatedCode, CorrectMethodType, CorrectPropertyType, CorrectImplements);
            CorrectCoreTypesForBackingStore(generatedCode, "BackingStoreFactorySingleton.instance.createBackingStore()");
            cancellationToken.ThrowIfCancellationRequested();
            AddInnerClasses(generatedCode,
                true,
                string.Empty,
                true);
            // `AddInnerClasses` will have inner classes moved to their own files, so  we add the imports after so that the files don't miss anything.
            // This is because imports are added at the file level so nested classes would potentially use the higher level imports.
            AddDefaultImports(generatedCode, defaultUsingEvaluators);
            DisableActionOf(generatedCode,
                CodeParameterKind.RequestConfiguration);
            cancellationToken.ThrowIfCancellationRequested();
            AddPropertiesAndMethodTypesImports(generatedCode, true, true, true);
            AliasUsingsWithSameSymbol(generatedCode);
            AddParsableImplementsForModelClasses(generatedCode, "Parsable");
            ReplaceBinaryByNativeType(generatedCode, "ArrayBuffer", null, isNullable: true);
            cancellationToken.ThrowIfCancellationRequested();
            ReplaceReservedNames(generatedCode, new TypeScriptReservedNamesProvider(), x => $"{x}_escaped");
            //AddGetterAndSetterMethods(generatedCode,
            //    new() {
            //        CodePropertyKind.Custom,
            //        CodePropertyKind.AdditionalData,
            //    },
            //    _configuration.UsesBackingStore,
            //    false,
            //    string.Empty,
            //    string.Empty);
            AddConstructorsForDefaultValues(generatedCode, true);
            cancellationToken.ThrowIfCancellationRequested();
            var defaultConfiguration = new GenerationConfiguration();
            ReplaceDefaultSerializationModules(
                generatedCode,
                defaultConfiguration.Serializers,
                new(StringComparer.OrdinalIgnoreCase) {
                    "@microsoft/kiota-serialization-json.JsonSerializationWriterFactory",
                    "@microsoft/kiota-serialization-text.TextSerializationWriterFactory"
                }
            );
            ReplaceDefaultDeserializationModules(
                generatedCode,
                defaultConfiguration.Deserializers,
                new(StringComparer.OrdinalIgnoreCase) {
                    "@microsoft/kiota-serialization-json.JsonParseNodeFactory",
                    "@microsoft/kiota-serialization-text.TextParseNodeFactory"
                }
            );
            AddSerializationModulesImport(generatedCode,
                new[] { $"{AbstractionsPackageName}.registerDefaultSerializer",
                        $"{AbstractionsPackageName}.enableBackingStoreForSerializationWriterFactory",
                        $"{AbstractionsPackageName}.SerializationWriterFactoryRegistry"},
                new[] { $"{AbstractionsPackageName}.registerDefaultDeserializer",
                        $"{AbstractionsPackageName}.ParseNodeFactoryRegistry" });
            cancellationToken.ThrowIfCancellationRequested();
            AddParentClassToErrorClasses(
                    generatedCode,
                    "ApiError",
                    "@microsoft/kiota-abstractions"
            );
            //AddDiscriminatorMappingsUsingsToParentClasses(
            //    generatedCode,
            //    "ParseNode",
            //    addUsings: false
            //);
            //Func<string, string> factoryNameCallbackFromTypeName = static x => $"create{x.ToFirstCharacterUpperCase()}FromDiscriminatorValue";
            //ReplaceLocalMethodsByGlobalFunctions(
            //    generatedCode,
            //    x => factoryNameCallbackFromTypeName(x.Parent.Name),
            //    x => x.Parent is CodeClass parentClass ? new List<CodeUsing>(parentClass.DiscriminatorInformation
            //                            .DiscriminatorMappings
            //                            .Select(static y => y.Value)
            //                            .OfType<CodeType>()
            //                            .Select(static y => new CodeUsing { Name = y.Name, Declaration = y })) {
            //            new() { Name = "ParseNode", Declaration = new() { Name = AbstractionsPackageName, IsExternal = true } },
            //            new() { Name = x.Parent.Parent.Name, Declaration = new() { Name = x.Parent.Name, TypeDefinition = x.Parent } },
            //        }.ToArray() : Array.Empty<CodeUsing>(),
            //    CodeMethodKind.Factory
            //);
            //Func<CodeType, string> factoryNameCallbackFromType = x => factoryNameCallbackFromTypeName(x.Name);
            cancellationToken.ThrowIfCancellationRequested();
            //AddStaticMethodsUsingsForDeserializer(
            //    generatedCode,
            //    factoryNameCallbackFromType
            //);
            //AddStaticMethodsUsingsForRequestExecutor(
            //    generatedCode,
            //    factoryNameCallbackFromType
            //);
            AddQueryParameterMapperMethod(
                generatedCode
            );


            CreateSeparateSerializers(generatedCode);
            AddModelsInterfaces(generatedCode);
            ReplaceRequestConfigurationsQueryParamsWithInterfaces(generatedCode);
        }, cancellationToken);
    }

    private static readonly CodeUsingDeclarationNameComparer usingComparer = new();
    private static void AliasUsingsWithSameSymbol(CodeElement currentElement)
    {
        if (currentElement is CodeClass currentClass &&
            currentClass.StartBlock is ClassDeclaration currentDeclaration &&
            currentDeclaration.Usings.Any(x => !x.IsExternal))
        {
            var duplicatedSymbolsUsings = currentDeclaration.Usings.Where(x => !x.IsExternal)
                                                                    .Distinct(usingComparer)
                                                                    .GroupBy(x => x.Declaration.Name, StringComparer.OrdinalIgnoreCase)
                                                                    .Where(x => x.Count() > 1)
                                                                    .SelectMany(x => x)
                                                                    .Union(currentDeclaration
                                                                            .Usings
                                                                            .Where(x => !x.IsExternal)
                                                                            .Where(x => x.Declaration
                                                                                            .Name
                                                                                            .Equals(currentClass.Name, StringComparison.OrdinalIgnoreCase)));
            foreach (var usingElement in duplicatedSymbolsUsings)
                usingElement.Alias = (usingElement.Declaration
                                                .TypeDefinition
                                                .GetImmediateParentOfType<CodeNamespace>()
                                                .Name +
                                    usingElement.Declaration
                                                .TypeDefinition
                                                .Name)
                                    .GetNamespaceImportSymbol();
        }
        CrawlTree(currentElement, AliasUsingsWithSameSymbol);
    }
    private const string AbstractionsPackageName = "@microsoft/kiota-abstractions";
    private static readonly AdditionalUsingEvaluator[] defaultUsingEvaluators = {
        new (x => x is CodeProperty prop && prop.IsOfKind(CodePropertyKind.RequestAdapter),
            AbstractionsPackageName, "RequestAdapter"),
        new (x => x is CodeProperty prop && prop.IsOfKind(CodePropertyKind.Options),
            AbstractionsPackageName, "RequestOption"),
        new (x => x is CodeMethod method && method.IsOfKind(CodeMethodKind.RequestGenerator),
            AbstractionsPackageName, "HttpMethod", "RequestInformation", "RequestOption"),
        new (x => x is CodeMethod method && method.IsOfKind(CodeMethodKind.RequestExecutor),
            AbstractionsPackageName, "ResponseHandler"),
        new (x => x is CodeMethod method && method.IsOfKind(CodeMethodKind.Serializer),
            AbstractionsPackageName, "SerializationWriter"),
        new (x => x is CodeMethod method && method.IsOfKind(CodeMethodKind.Deserializer, CodeMethodKind.Factory),
            AbstractionsPackageName, "ParseNode"),
        new (x => x is CodeMethod method && method.IsOfKind(CodeMethodKind.Constructor, CodeMethodKind.ClientConstructor, CodeMethodKind.IndexerBackwardCompatibility),
            AbstractionsPackageName, "getPathParameters"),
        new (x => x is CodeMethod method && method.IsOfKind(CodeMethodKind.RequestExecutor),
            AbstractionsPackageName, "Parsable", "ParsableFactory"),
        new (x => x is CodeClass @class && @class.IsOfKind(CodeClassKind.Model),
            AbstractionsPackageName, "Parsable"),
        new (x => x is CodeClass @class && @class.IsOfKind(CodeClassKind.Model) && @class.Properties.Any(x => x.IsOfKind(CodePropertyKind.AdditionalData)),
            AbstractionsPackageName, "AdditionalDataHolder"),
        new (x => x is CodeMethod method && method.IsOfKind(CodeMethodKind.ClientConstructor) &&
                    method.Parameters.Any(y => y.IsOfKind(CodeParameterKind.BackingStore)),
            AbstractionsPackageName, "BackingStoreFactory", "BackingStoreFactorySingleton"),
        new (x => x is CodeProperty prop && prop.IsOfKind(CodePropertyKind.BackingStore),
            AbstractionsPackageName, "BackingStore", "BackedModel", "BackingStoreFactorySingleton" ),
    };
    private static void CorrectImplements(ProprietableBlockDeclaration block)
    {
        block.ReplaceImplementByName("IAdditionalDataHolder", "AdditionalDataHolder");
    }
    private static void CorrectPropertyType(CodeProperty currentProperty)
    {
        if (currentProperty.IsOfKind(CodePropertyKind.RequestAdapter))
            currentProperty.Type.Name = "RequestAdapter";
        else if (currentProperty.IsOfKind(CodePropertyKind.BackingStore))
            currentProperty.Type.Name = currentProperty.Type.Name[1..]; // removing the "I"
        else if (currentProperty.IsOfKind(CodePropertyKind.Options))
            currentProperty.Type.Name = "RequestOption[]";
        else if (currentProperty.IsOfKind(CodePropertyKind.Headers))
            currentProperty.Type.Name = "Record<string, string>";
        else if (currentProperty.IsOfKind(CodePropertyKind.AdditionalData))
        {
            currentProperty.Type.Name = "Record<string, unknown>";
            currentProperty.DefaultValue = "{}";
            currentProperty.Type.IsNullable = true;
        }
        else if (currentProperty.IsOfKind(CodePropertyKind.PathParameters))
        {
            currentProperty.Type.IsNullable = false;
            currentProperty.Type.Name = "Record<string, unknown>";
            if (!string.IsNullOrEmpty(currentProperty.DefaultValue))
                currentProperty.DefaultValue = "{}";
        }
        CorrectDateTypes(currentProperty.Parent as CodeClass, DateTypesReplacements, currentProperty.Type);
    }
    private static void CorrectMethodType(CodeMethod currentMethod)
    {
        if (currentMethod.IsOfKind(CodeMethodKind.RequestExecutor, CodeMethodKind.RequestGenerator))
        {
            if (currentMethod.IsOfKind(CodeMethodKind.RequestExecutor))
                currentMethod.Parameters.Where(x => x.IsOfKind(CodeParameterKind.ResponseHandler) && x.Type.Name.StartsWith("i", StringComparison.OrdinalIgnoreCase)).ToList().ForEach(x => x.Type.Name = x.Type.Name[1..]);
        }
        else if (currentMethod.IsOfKind(CodeMethodKind.Serializer))
            currentMethod.Parameters.Where(x => x.IsOfKind(CodeParameterKind.Serializer) && x.Type.Name.StartsWith("i", StringComparison.OrdinalIgnoreCase)).ToList().ForEach(x => x.Type.Name = x.Type.Name[1..]);
        else if (currentMethod.IsOfKind(CodeMethodKind.Deserializer))
            currentMethod.ReturnType.Name = "Record<string, (node: ParseNode) => void>";
        else if (currentMethod.IsOfKind(CodeMethodKind.ClientConstructor, CodeMethodKind.Constructor))
        {
            currentMethod.Parameters.Where(x => x.IsOfKind(CodeParameterKind.RequestAdapter, CodeParameterKind.BackingStore))
                .Where(x => x.Type.Name.StartsWith("I", StringComparison.InvariantCultureIgnoreCase))
                .ToList()
                .ForEach(x => x.Type.Name = x.Type.Name[1..]); // removing the "I"
            var urlTplParams = currentMethod.Parameters.FirstOrDefault(x => x.IsOfKind(CodeParameterKind.PathParameters));
            if (urlTplParams != null &&
                urlTplParams.Type is CodeType originalType)
            {
                originalType.Name = "Record<string, unknown>";
                urlTplParams.Description = "The raw url or the Url template parameters for the request.";
                var unionType = new CodeUnionType
                {
                    Name = "rawUrlOrTemplateParameters",
                    IsNullable = true,
                };
                unionType.AddType(originalType, new()
                {
                    Name = "string",
                    IsNullable = true,
                    IsExternal = true,
                });
                urlTplParams.Type = unionType;
            }
        }
        else if (currentMethod.IsOfKind(CodeMethodKind.Factory) && currentMethod.Parameters.OfKind(CodeParameterKind.ParseNode) is CodeParameter parseNodeParam)
            parseNodeParam.Type.Name = parseNodeParam.Type.Name[1..];
        CorrectDateTypes(currentMethod.Parent as CodeClass, DateTypesReplacements, currentMethod.Parameters
                                                .Select(x => x.Type)
                                                .Union(new[] { currentMethod.ReturnType })
                                                .ToArray());
    }
    private static readonly Dictionary<string, (string, CodeUsing)> DateTypesReplacements = new(StringComparer.OrdinalIgnoreCase)
    {
        {
            "DateTimeOffset",
            ("Date", null)
        },
        {
            "TimeSpan",
            ("Duration", new CodeUsing
            {
                Name = "Duration",
                Declaration = new CodeType
                {
                    Name = AbstractionsPackageName,
                    IsExternal = true,
                },
            })
        },
        {
            "DateOnly",
            (null, new CodeUsing
            {
                Name = "DateOnly",
                Declaration = new CodeType
                {
                    Name = AbstractionsPackageName,
                    IsExternal = true,
                },
            })
        },
        {
            "TimeOnly",
            (null, new CodeUsing
            {
                Name = "TimeOnly",
                Declaration = new CodeType
                {
                    Name = AbstractionsPackageName,
                    IsExternal = true,
                },
            })
        },
    };

    private static void ReplaceRequestConfigurationsQueryParamsWithInterfaces(CodeElement currentElement)
    {
        if (currentElement is CodeClass codeClass && codeClass.IsOfKind(CodeClassKind.QueryParameters, CodeClassKind.RequestConfiguration))
        {

            var targetNS = codeClass.GetImmediateParentOfType<CodeNamespace>();
            var existing = targetNS.FindChildByName<CodeInterface>(codeClass.Name, false);
            if (existing != null)
                return;

            var insertValue = new CodeInterface
            {
                Name = codeClass.Name,
                Kind = codeClass.IsOfKind(CodeClassKind.QueryParameters) ? CodeInterfaceKind.QueryParameters : CodeInterfaceKind.RequestConfiguration
            };
            targetNS.RemoveChildElement(codeClass);
            var codeInterface = targetNS.AddInterface(insertValue).First();
            var props = codeClass.Properties?.ToArray();
            if (props.Any())
            {
                codeInterface.AddProperty(props);
            }
            var usings = codeClass.Usings?.ToArray();
            if (usings.Any())
            {
                codeInterface.AddUsing(usings);
            }
        }
        CrawlTree(currentElement, x => ReplaceRequestConfigurationsQueryParamsWithInterfaces(x));
    }
    private const string TemporaryInterfaceNameSuffix = "Interface";
    private const string FinalModelClassNameSuffix = "Impl";
    private const string ModelSerializerPrefix = "serialize";
    private const string ModelDeserializerPrefix = "deserializeInto";

    private static void CreateSeparateSerializers(CodeElement codeElement)
    {
        if (codeElement is CodeClass codeClass && codeClass.Kind == CodeClassKind.Model)
        {
            CreateSerializationFunctions(codeClass);
        }
        CrawlTree(codeElement, x => CreateSeparateSerializers(x));
    }


    private static void CreateSerializationFunctions(CodeClass modelClass)
    {

        var targetNS = modelClass.GetImmediateParentOfType<CodeNamespace>();
        var serializer = modelClass.Methods.FirstOrDefault(x => x.Kind == CodeMethodKind.Serializer);

        var deserializer = modelClass.Methods.FirstOrDefault(x => x.Kind == CodeMethodKind.Deserializer);
        serializer.IsStatic = true;
        deserializer.IsStatic = true;

        var globalSerializer = new CodeFunction(serializer)
        {
            Name = $"{ModelSerializerPrefix}{modelClass.Name.ToFirstCharacterUpperCase()}",
        };

        var globalDeserializer = new CodeFunction(deserializer)
        {
            Name = $"{ModelDeserializerPrefix}{modelClass.Name.ToFirstCharacterUpperCase()}",
        };

        //  var modelInterface = targetNS.FindChildByName<CodeInterface>(modelClass.Name + TemporaryInterfaceNameSuffix);
        //var param = new CodeParameter
        //{
        //    Name = modelClass.Name,
        //    DefaultValue = "{}",
        //    Type = new CodeType { Name = modelClass.Name + TemporaryInterfaceNameSuffix, TypeDefinition = modelInterface }

        //};

        //serializer.AddParameter(param);
        //deserializer.AddParameter(param);

        foreach (var codeUsing in modelClass.Usings.Where(x => x.Declaration.IsExternal)){

            globalDeserializer.AddUsing(codeUsing);
            globalSerializer.AddUsing(codeUsing);
        }

        targetNS.AddFunction(globalDeserializer);
        targetNS.AddFunction(globalSerializer);

    }

    private static void AddInterfaceParamToSerializer(CodeInterface modelInterface, CodeFunction codeFunction)
    {
        var method = codeFunction.OriginalLocalMethod;

        method.AddParameter(new CodeParameter
        {
            Name = ReturnFinalInterfaceName(modelInterface.Name), // remove the interface suffix
            DefaultValue = "{}",
            Type = new CodeType { Name = ReturnFinalInterfaceName(modelInterface.Name), TypeDefinition = modelInterface }
        });

        codeFunction.AddUsing(new CodeUsing
        {
            Name = modelInterface.Parent.Name,
            Declaration = new CodeType
            {
                Name = ReturnFinalInterfaceName(modelInterface.Name),
                TypeDefinition = modelInterface
            }

        });
    }


    //private static void AddUsingForCodeFunctions(CodeElement generatedCode)
    //{
    //    if (generatedCode is CodeFunction codeFunction && codeFunction.OriginalLocalMethod.IsOfKind(CodeMethodKind.Serializer, CodeMethodKind.Deserializer))
    //    {

    //        var param = codeFunction.OriginalLocalMethod.Parameters.FirstOrDefault(x => (x.Type as CodeType).TypeDefinition is CodeInterface);
    //        var codeInterface = (param.Type as CodeType).TypeDefinition as CodeInterface;

    //        var inherits = codeInterface.StartBlock.Implements.Where(x => x.TypeDefinition is CodeInterface);
    //        if (codeFunction.OriginalLocalMethod.Kind == CodeMethodKind.Deserializer)
    //        {
    //            AddDeserializerReference(codeFunction, CodeClass, generatedCode.GetImmediateParentOfType<CodeNamespace>(), inherits);
    //        }
    //        if (codeFunction.OriginalLocalMethod.Kind == CodeMethodKind.Serializer)
    //        {
    //            AddSerializerReference(codeFunction, codeInterface, generatedCode.GetImmediateParentOfType<CodeNamespace>());
    //        }


    //    }

    //    CrawlTree(generatedCode, x => AddUsingForCodeFunctions(x));
    //}

    private static void AddDeserializerReference(CodeFunction function, CodeClass property, CodeNamespace targetNS, IEnumerable<CodeType> inherits)
    {
        // find child in other namespace
        foreach (var parent in inherits)
        {

        }
        foreach (var prop in property.Properties.Where(x => x.IsOfKind(CodePropertyKind.Custom)))
        {
            if (prop.Type is CodeType codeType && codeType.TypeDefinition != null)
            {
                var name = $"deserializeInto{codeType.TypeDefinition.Name.ToFirstCharacterUpperCase()}";
                var code = targetNS.Functions.FirstOrDefault(x => x.Name == $"{name?.ToFirstCharacterUpperCase()}");
                if (code != null)
                {
                    Console.WriteLine("prop.Name" + prop.Name);
                    function.AddUsing(new CodeUsing
                    {
                        Name = targetNS.Name,

                        Declaration = new CodeType
                        {
                            Name = name,

                            TypeDefinition = code
                        }
                    });
                }
            }
        }
    }

    private static void AddSerializerReference(CodeFunction function, CodeClass property, CodeNamespace targetNS)
    {
        foreach (var prop in property.Properties.Where(x => x.IsOfKind(CodePropertyKind.Custom)))
        {
            if (prop.Type is CodeType codeType && codeType.TypeDefinition != null)
            {
                var name = $"serialize{codeType.TypeDefinition.Name.ToFirstCharacterUpperCase()}";
                var code = targetNS.Functions.FirstOrDefault(x => x.Name == $"{name?.ToFirstCharacterUpperCase()}");
                if (code != null)
                {
                    function.AddUsing(new CodeUsing
                    {
                        Name = targetNS.Name,
                        Declaration = new CodeType
                        {
                            Name = name,
                            TypeDefinition = code
                        }
                    });
                }
            }
        }
    }

    private static void AddModelsInterfaces(CodeElement generatedCode)
    {
        GenerateModelInterfaces(
           generatedCode,
           x => $"{x.Name.ToFirstCharacterUpperCase()}Interface".ToFirstCharacterUpperCase()
       );

        RenameModelInterfacesAndClasses(generatedCode);

    }

    /// <summary>
    /// Removes the "Interface" suffix temporarily added to model interface names
    /// Adds the "Impl" suffix to model class names.
    /// </summary>
    /// <param name="currentElement"></param>
    private static void RenameModelInterfacesAndClasses(CodeElement currentElement)
    {
        if (currentElement is CodeClass currentClass && currentClass.IsOfKind(CodeClassKind.Model) && !currentClass.Name.EndsWith(FinalModelClassNameSuffix))
        {
            var targetNS = currentClass.GetImmediateParentOfType<CodeNamespace>();
            var existing = targetNS.FindChildByName<CodeInterface>(currentClass.Name, false);
            if (existing != null)
                return;
            targetNS.RemoveChildElement(currentElement);
            //currentClass.Name = currentClass.Name + FinalModelClassNameSuffix;
        }
        else if (currentElement is CodeInterface modelInterface && modelInterface.IsOfKind(CodeInterfaceKind.Model) && modelInterface.Name.EndsWith(TemporaryInterfaceNameSuffix))
        {
            modelInterface.Name = ReturnFinalInterfaceName(modelInterface.Name);
        }
        else if (currentElement is CodeUsing codeUsing)
        {
            if (codeUsing.Declaration.TypeDefinition is CodeInterface codeInterface) {
                codeUsing.Declaration.Name = ReturnFinalInterfaceName(codeInterface.Name);
                codeInterface.Name = ReturnFinalInterfaceName(codeInterface.Name);
            }
            else if (codeUsing.Declaration.TypeDefinition is CodeClass codeClass && codeClass.Kind == CodeClassKind.Model)
            {

                (codeUsing.Parent as CodeClass).RemoveUsingsByDeclarationName(codeClass.Name);
            }
        }
        else if (currentElement is CodeFunction codeFunction && codeFunction.OriginalLocalMethod.IsOfKind(CodeMethodKind.Serializer, CodeMethodKind.Deserializer)) {

            var param = codeFunction.OriginalLocalMethod.Parameters.FirstOrDefault(x => (x.Type as CodeType).TypeDefinition is CodeInterface);
           
            var paramInterface = (param.Type as CodeType).TypeDefinition as CodeInterface;
            paramInterface.Name = ReturnFinalInterfaceName(paramInterface.Name);
            param.Name = ReturnFinalInterfaceName(paramInterface.Name); 
        }

        CrawlTree(currentElement, x => RenameModelInterfacesAndClasses(x));
    }

    private static string ReturnFinalInterfaceName(string interfaceName)
    {
        return interfaceName.Split(TemporaryInterfaceNameSuffix)[0];
    }

    private static void GenerateModelInterfaces(CodeElement currentElement, Func<CodeClass, string> interfaceNamingCallback)
    {
        if (currentElement is CodeClass currentClass && currentClass.IsOfKind(CodeClassKind.Model))
        {
            var modelInterface = CreateModelInterface(currentClass, interfaceNamingCallback);

            var serializer = (currentClass.Parent as CodeNamespace).FindChildByName<CodeFunction>($"{ModelSerializerPrefix}{currentClass.Name}");
            var deserializer = (currentClass.Parent as CodeNamespace).FindChildByName<CodeFunction>($"{ModelDeserializerPrefix}{currentClass.Name}");
            AddInterfaceParamToSerializer(modelInterface, serializer);
            AddInterfaceParamToSerializer(modelInterface, deserializer);
        }
        /*
         * Setting object property type to interface type.
         */
        else if (currentElement is CodeProperty codeProperty &&
                codeProperty.IsOfKind(CodePropertyKind.RequestBody) &&
                codeProperty.Type is CodeType type &&
                type.TypeDefinition is CodeClass modelClass &&
                modelClass.IsOfKind(CodeClassKind.Model))
        {
            SetTypeAndAddUsing(CreateModelInterface(modelClass, interfaceNamingCallback), type, codeProperty);
            
        }
        else if (currentElement is CodeMethod codeMethod && codeMethod.IsOfKind(CodeMethodKind.RequestExecutor, CodeMethodKind.RequestGenerator))
        {
            ProcessModelClassesAssociatedWithMethods(codeMethod, interfaceNamingCallback);

        }

        CrawlTree(currentElement, x => GenerateModelInterfaces(x, interfaceNamingCallback));
    }


    private static void AddSerializationUsingToRequestBuilder(CodeClass modelClass, CodeClass targetClass)
    {
        var serializer = (modelClass.Parent as CodeNamespace).FindChildByName<CodeFunction>($"{ModelSerializerPrefix}{modelClass.Name.ToFirstCharacterUpperCase()}");
        var deserializer = (modelClass.Parent as CodeNamespace).FindChildByName<CodeFunction>($"{ModelDeserializerPrefix}{modelClass.Name.ToFirstCharacterUpperCase()}");

        
        targetClass.AddUsing(new CodeUsing
         {
             Name = serializer.Parent.Name,
             Declaration = new CodeType
             {
                 Name = serializer.Name,
                 TypeDefinition = serializer
             }
         });

        targetClass.AddUsing(new CodeUsing
         {
             Name = deserializer.Parent.Name,
             Declaration = new CodeType
             {
                 Name = deserializer.Name,
                 TypeDefinition = deserializer
             }
         });
    }

    private static void ProcessModelClassesAssociatedWithMethods(CodeMethod codeMethod, Func<CodeClass, string> interfaceNamingCallback)
    {
        /*
         * Setting request body parameter type of request executor to model interface.
         */

        var parentClass = codeMethod.GetImmediateParentOfType<CodeClass>();
        if (codeMethod.ReturnType is CodeType returnType &&
            returnType.TypeDefinition is CodeClass returnClass &&
            returnClass.IsOfKind(CodeClassKind.Model) && parentClass != null && parentClass.Name != returnClass.Name)
        {
            AddSerializationUsingToRequestBuilder(returnClass, codeMethod.Parent as CodeClass);
            // parentClass.AddUsing(new CodeUsing { Name = returnClass.Parent.Name, Declaration = new CodeType { Name = returnClass.Name, TypeDefinition = returnClass } });
            var bodyType = codeMethod?.Parameters?.FirstOrDefault(x => x.Kind == CodeParameterKind.RequestBody);

            if ( bodyType != null ) {
                var bodyClass = (bodyType.Type as CodeType).TypeDefinition as CodeClass;
                if ( bodyClass!=null && bodyClass!= returnClass) {
                    AddSerializationUsingToRequestBuilder(bodyClass, codeMethod.Parent as CodeClass);
                }
            }
        }
   
        var requestBodyParam = codeMethod?.Parameters?.OfKind(CodeParameterKind.RequestBody);
        if (requestBodyParam != null && requestBodyParam.Type is CodeType paramType && paramType.TypeDefinition is CodeClass paramClass)
        {
            //SetTypeAndAddUsing(CreateModelInterface(paramType.TypeDefinition as CodeClass, interfaceNamingCallback), paramType, requestBodyParam);
            if (parentClass != null && parentClass.Name != paramClass.Name)
            {
                parentClass.AddUsing(new CodeUsing { Name = paramClass.Parent.Name, Declaration = new CodeType { Name = paramClass.Name, TypeDefinition = paramClass } });

            }
        }
    }
    private static void SetTypeAndAddUsing(CodeInterface interfaceElement, CodeType elemType, CodeElement targetElement)
    {
        if (interfaceElement.Name.EndsWith(TemporaryInterfaceNameSuffix))
        {
            elemType.Name = interfaceElement.Name.Split(TemporaryInterfaceNameSuffix)[0];
        }
        else
        {
            elemType.Name = interfaceElement.Name;

        }
        elemType.TypeDefinition = interfaceElement;
        var nameSpaceOfInterface = interfaceElement.GetImmediateParentOfType<CodeNamespace>();
        if (nameSpaceOfInterface != targetElement.GetImmediateParentOfType<CodeNamespace>())
        {
            var targetClass = targetElement.GetImmediateParentOfType<CodeClass>();
            if (targetClass.Parent is CodeClass parentClass)
                targetClass = parentClass;
            targetClass.AddUsing(new CodeUsing
            {
                Name = nameSpaceOfInterface.Name,
                Declaration = new CodeType
                {
                    Name = interfaceElement.Name,
                    TypeDefinition = interfaceElement,
                },
            });

            targetClass.AddUsing();
        }
    }
    private static CodeInterface CreateModelInterface(CodeClass modelClass, Func<CodeClass, string> interfaceNamingCallback)
    {
        /*
         * Temporarily name the interface with "Interface" suffix 
         * since adding code elements of the same name in the same namespace causes error. 
         */

        var temporaryInterfaceName = interfaceNamingCallback.Invoke(modelClass);
        var namespaceOfModel = modelClass.GetImmediateParentOfType<CodeNamespace>();
        var existing = namespaceOfModel.FindChildByName<CodeInterface>(temporaryInterfaceName, false);
        if (existing != null)
            return existing;
        var modelParentClass = modelClass.Parent as CodeClass;
        var shouldInsertUnderParentClass = modelParentClass != null;
        var insertValue = new CodeInterface
        {
            Name = temporaryInterfaceName,
            Kind = CodeInterfaceKind.Model,
        };

        var modelInterface = shouldInsertUnderParentClass ?
                       modelParentClass.AddInnerInterface(insertValue).First() :
                       namespaceOfModel.AddInterface(insertValue).First();

        var classModelChildItems = modelClass.GetChildElements(true);

        var props = classModelChildItems.OfType<CodeProperty>();
        var methods = classModelChildItems.OfType<CodeMethod>();
        ProcessModelClassDeclaration(modelClass, modelInterface, interfaceNamingCallback); 
        ProcessModelClassProperties(modelClass, modelInterface, props, interfaceNamingCallback);
        //ProcessModelClassMethods(modelClass, methods, interfaceNamingCallback);

        return modelInterface;
    }

    private static void ProcessModelClassDeclaration(CodeClass modelClass, CodeInterface modelInterface, Func<CodeClass, string> tempInterfaceNamingCallback)
    {
        /*
         * If a child model class inherits from parent model class, the child interface extends from the parent interface.
         */
        if (modelClass.StartBlock.Inherits?.TypeDefinition is CodeClass baseClass)
        {
            var parentInterface = CreateModelInterface(baseClass, tempInterfaceNamingCallback);
            modelInterface.StartBlock.AddImplements(new CodeType
            {
                Name = parentInterface.Name,
                TypeDefinition = parentInterface,
            });
            var parentInterfaceNS = parentInterface.GetImmediateParentOfType<CodeNamespace>();

            modelInterface.AddUsing(new CodeUsing
            {
                Name = parentInterfaceNS.Name,
                Declaration = new CodeType
                {
                    Name = ReturnFinalInterfaceName(parentInterface.Name),
                    TypeDefinition = parentInterface,
                },
            });
            AddSuperInSerializerFunction(modelClass, baseClass);
        }

        foreach (var impl in modelClass.StartBlock.Implements)
        {

            modelInterface.StartBlock.AddImplements(new CodeType
            {
                Name = impl.Name,
                TypeDefinition = impl.TypeDefinition,
            });

            modelInterface.AddUsing(modelClass.Usings.FirstOrDefault(x => x.Name == impl.Name));
            //modelClass.StartBlock.RemoveImplements(impl);
        }
        //UpdateModelClassImplementationAndConstructor(modelClass, modelInterface);

    }

    private static void AddSuperInSerializerFunction(CodeClass childClass, CodeClass parentClass)
    {
        var serializer = (childClass.Parent as CodeNamespace).FindChildByName<CodeFunction>($"{ModelSerializerPrefix}{childClass.Name.ToFirstCharacterUpperCase()}");
        var deserializer = (childClass.Parent as CodeNamespace).FindChildByName<CodeFunction>($"{ModelDeserializerPrefix}{childClass.Name.ToFirstCharacterUpperCase()}");

        var parentSerializer = (parentClass.Parent as CodeNamespace).FindChildByName<CodeFunction>($"{ModelSerializerPrefix}{parentClass.Name.ToFirstCharacterUpperCase()}");
        var parentDeserializer = (parentClass.Parent as CodeNamespace).FindChildByName<CodeFunction>($"{ModelDeserializerPrefix}{parentClass.Name.ToFirstCharacterUpperCase()}");

        serializer.AddUsing(new CodeUsing
        {
            Name = parentSerializer.Parent.Name,
            Declaration = new CodeType { 
                Name = parentSerializer.Name,
                TypeDefinition = parentSerializer
            }
        });

        deserializer.AddUsing(new CodeUsing
        {
            Name = parentDeserializer.Parent.Name,
            Declaration = new CodeType
            {
                Name = parentDeserializer.Name,
                TypeDefinition = parentDeserializer
            }
        });
    }

    private static void UpdateModelClassImplementationAndConstructor(CodeClass modelClass, CodeInterface modelInterface)
    {

        var finalInterfaceName = ReturnFinalInterfaceName(modelInterface.Name);
        /*
         * Model class should implement the model interface
         */
        modelClass.StartBlock.AddImplements(new CodeType
        {
            Name = finalInterfaceName,
            TypeDefinition = modelInterface,
        });

        /*
         * Set a parameter of model interface type in the model class constructor
         */
        var constructor = modelClass.Methods?.FirstOrDefault(x => x is CodeMethod method1 && method1.IsOfKind(CodeMethodKind.Constructor));
        if (constructor == null)
        {
            constructor = modelClass.AddMethod(new CodeMethod
            {
                Name = "constructor",
                Kind = CodeMethodKind.Constructor,
                IsAsync = false,
                IsStatic = false,
                Description = $"Instantiates a new {modelClass.Name.ToFirstCharacterUpperCase()} and sets the default values.",
                Access = AccessModifier.Public,
            }).First();

        }
        constructor.AddParameter(new CodeParameter
        {
            Name = finalInterfaceName.ToFirstCharacterLowerCase() + "ParameterValue",
            Type = new CodeType { Name = finalInterfaceName, TypeDefinition = modelInterface },
            Optional = true
        });

        modelClass.AddUsing(new CodeUsing
        {

            Name = modelInterface.Parent.Name,
            Declaration = new CodeType
            {
                Name = finalInterfaceName,
                TypeDefinition = modelInterface,
            }
        });
    }
    private static void ProcessModelClassMethods(CodeClass modelClass, IEnumerable<CodeMethod> methods, Func<CodeClass, string> interfaceNamingCallback)
    {
        foreach (var method in methods)
        {
            if (method.ReturnType is CodeType methodReturnType)
            {
                ProcessModelClassMethodParamAndReturnType(modelClass, methodReturnType, interfaceNamingCallback);
            }

            foreach (var parameter in method.Parameters)
                if (parameter.Type is CodeType parameterType &&
                    !parameterType.IsExternal)
                {
                    ProcessModelClassMethodParamAndReturnType(modelClass, parameterType, interfaceNamingCallback);
                }
        }
    }

    private static void ProcessModelClassMethodParamAndReturnType(CodeClass modelClass, CodeType codeType, Func<CodeClass, string> interfaceNamingCallback)
    {
        if (!codeType.IsExternal && codeType.TypeDefinition is CodeClass codeClass)
        {
            var resultType = ReturnUpdatedModelInterfaceTypeAndUsing(codeClass, codeType, interfaceNamingCallback);
            modelClass.AddUsing(resultType.Item2);
        }
    }

    private static void UpdatePropertyTypeInModelClass(CodeClass modelClass, CodeClass propertyClass, (CodeInterface, CodeUsing) propertyTypeAndUsing)
    {
        modelClass.AddUsing(propertyTypeAndUsing.Item2);
        /***
        * Append "impl" and add class property type in  the model class usings as they will be required by the serializer method. 
        */
        if (modelClass.Name != propertyClass.Name)
        {
            modelClass.AddUsing(new CodeUsing
            {
                Name = propertyClass.Parent.Name,
                Declaration = new CodeType
                {
                    Name = propertyClass.Name + (propertyClass.Name.EndsWith(FinalModelClassNameSuffix) ? string.Empty : FinalModelClassNameSuffix),
                    TypeDefinition = propertyClass,
                }
            });
        }
    }
    private static void SetUsingInModelInterface(CodeInterface modelInterface, (CodeInterface, CodeUsing) propertyTypeAndUsing)
    {
        if (modelInterface.Name != propertyTypeAndUsing.Item1.Name)
        {
            modelInterface.AddUsing(propertyTypeAndUsing.Item2);
        }
    }

    private static void SetUsingInSerializerForProperty(CodeFunction codeFunction, CodeClass property)
    {
        var name = $"{ModelSerializerPrefix}{property.Name.ToFirstCharacterUpperCase()}";
        // check if function is not same as the prop
        if (name != codeFunction.Name)
        {
            // check if function is not same as the prop
            var serializer = (property.Parent as CodeNamespace).FindChildByName<CodeFunction>(name);

            codeFunction.AddUsing(new CodeUsing
            {
                Name = serializer.Parent.Name,
                Declaration = new CodeType
                {
                    Name = serializer.Name,
                    TypeDefinition = serializer

                }
            });
        }
    }

    private static void SetUsingInDeserializerForProperty(CodeFunction codeFunction, CodeClass property)
    {
        var name = $"{ModelDeserializerPrefix}{property.Name.ToFirstCharacterUpperCase()}";
        // check if function is not same as the prop
        if (name != codeFunction.Name)
        {
            var deserializer = (property.Parent as CodeNamespace).FindChildByName<CodeFunction>(name);

            codeFunction.AddUsing(new CodeUsing
            {
                Name = deserializer.Parent.Name,
                Declaration = new CodeType
                {
                    Name = deserializer.Name,
                    TypeDefinition = deserializer

                }
            });

        }
    }


    private static void ProcessModelClassProperties(CodeClass modelClass, CodeInterface modelInterface, IEnumerable<CodeProperty> properties, Func<CodeClass, string> interfaceNamingCallback)
    {
        /*
         * Add properties to interfaces
         * Replace model classes by interfaces for property types 
         */

        var serializer = (modelClass.Parent as CodeNamespace).FindChildByName<CodeFunction>($"{ModelSerializerPrefix}{modelClass.Name.ToFirstCharacterUpperCase()}");
        var deserializer = (modelClass.Parent as CodeNamespace).FindChildByName<CodeFunction>($"{ModelDeserializerPrefix}{modelClass.Name.ToFirstCharacterUpperCase()}");

        foreach (var mProp in properties)
        {
            if (mProp.Type is CodeType nonModelClassType && (nonModelClassType.IsExternal || (!(nonModelClassType.TypeDefinition is CodeClass))))
            {
                var usingExternal = modelClass.Usings.FirstOrDefault(x => (String.Equals(x.Name, nonModelClassType.Name, StringComparison.OrdinalIgnoreCase) || String.Equals(x.Declaration.Name, nonModelClassType.Name, StringComparison.OrdinalIgnoreCase)));

                if (usingExternal != null)
                {
                    modelInterface.AddUsing(usingExternal);
                    serializer.AddUsing(usingExternal);
                    deserializer.AddUsing(usingExternal);
                }

            }
            else if (mProp.Type is CodeType propertyType && propertyType.TypeDefinition is CodeClass propertyClass)
            {
                var interfaceTypeAndUsing = ReturnUpdatedModelInterfaceTypeAndUsing(propertyClass, propertyType, interfaceNamingCallback);
                SetUsingInModelInterface(modelInterface, interfaceTypeAndUsing);

                SetUsingInSerializerForProperty(serializer, propertyClass);
                SetUsingInDeserializerForProperty(deserializer, propertyClass);
                //UpdatePropertyTypeInModelClass(modelClass, propertyClass, interfaceTypeAndUsing);
            }

            var newProperty = mProp.Clone() as CodeProperty;
            modelInterface.AddProperty(newProperty);
        }
    }
    private static (CodeInterface, CodeUsing) ReturnUpdatedModelInterfaceTypeAndUsing(CodeClass sourceClass, CodeType originalType, Func<CodeClass, string> interfaceNamingCallback)
    {
        var propertyInterfaceType = CreateModelInterface(sourceClass, interfaceNamingCallback);
        originalType.Name = propertyInterfaceType.Name;
        originalType.TypeDefinition = propertyInterfaceType;
        return (propertyInterfaceType, new CodeUsing
        {
            Name = propertyInterfaceType.Parent.Name,
            Declaration = new CodeType
            {
                Name = propertyInterfaceType.Name,
                TypeDefinition = propertyInterfaceType,
            }
        });
    }

}
