

using System;
using System.Linq;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.TypeScript;

public class CodeFunctionWriter : BaseElementWriter<CodeFunction, TypeScriptConventionService>
{
    private TypeScriptConventionService localConventions;
    private readonly CodeUsingWriter _codeUsingWriter;
    public CodeFunctionWriter(TypeScriptConventionService conventionService, string clientNamespaceName) : base(conventionService)
    {
        _codeUsingWriter = new(clientNamespaceName);
    }

    public override void WriteCodeElement(CodeFunction codeElement, LanguageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(codeElement);
        if (codeElement.OriginalLocalMethod == null) throw new InvalidOperationException($"{nameof(codeElement.OriginalLocalMethod)} should not be null");
        ArgumentNullException.ThrowIfNull(writer);
        if (codeElement.Parent is not CodeNamespace) throw new InvalidOperationException("the parent of a function should be a namespace");
        _codeUsingWriter.WriteCodeElement(codeElement.StartBlock.Usings, codeElement.GetImmediateParentOfType<CodeNamespace>(), writer);

        var returnType = conventions.GetTypeString(codeElement.OriginalLocalMethod.ReturnType, codeElement);
        CodeMethodWriter.WriteMethodPrototypeInternal(codeElement.OriginalLocalMethod, writer, returnType, false, conventions, true);

        writer.IncreaseIndent();

        var codeMethod = codeElement.OriginalLocalMethod;
        localConventions = new TypeScriptConventionService(writer);
        if (codeMethod.Kind == CodeMethodKind.Deserializer)
        {
            WriteDeserializerMethod(codeElement, writer);
        }
        else
        if (codeMethod.Kind == CodeMethodKind.Serializer)
        {
            WriteSerializerMethod(codeElement, writer);
        }
        //else
        //{
        //    CodeMethodWriter.WriteDefensiveStatements(codeElement.OriginalLocalMethod, writer);
        //    WriteFactoryMethodBody(codeElement, returnType, writer);
        //}
    }

    private void WriteSerializerMethod(CodeFunction codeElement, LanguageWriter writer)
    {
        var param = codeElement.OriginalLocalMethod.Parameters.FirstOrDefault(x => (x.Type as CodeType).TypeDefinition is CodeInterface);
        var codeInterface = (param.Type as CodeType).TypeDefinition as CodeInterface;
        var inherits = codeInterface.StartBlock.Implements.FirstOrDefault(x => x.TypeDefinition is CodeInterface);
        writer.IncreaseIndent();

        if (inherits != null)
        {
            writer.WriteLine($"serialize{inherits.TypeDefinition.Name.ToFirstCharacterUpperCase()}(writer, {param.Name.ToFirstCharacterLowerCase()})");
        }

        foreach (var otherProp in codeInterface.Properties.Where(static x => x.Kind == CodePropertyKind.Custom && !x.ExistsInBaseType))
        {
            WritePropertySerializer(codeInterface.Name.ToFirstCharacterLowerCase(), otherProp, writer);
        }

        writer.DecreaseIndent();

    }

    private static bool IsCodePropertyCollectionOfEnum(CodeProperty property)
    {
        return property.Type is CodeType cType && cType.IsCollection && cType.TypeDefinition is CodeEnum;
    }
    private void WritePropertySerializer(string modelParamName, CodeProperty codeProperty, LanguageWriter writer)
    {
        var isCollectionOfEnum = IsCodePropertyCollectionOfEnum(codeProperty);
        var spreadOperator = isCollectionOfEnum ? "..." : string.Empty;
        var codePropertyName = codeProperty.Name.ToFirstCharacterLowerCase();
        var propertyTypeName = (codeProperty.Type as CodeType)?.TypeDefinition?.Name;
        writer.IncreaseIndent();

        var serializationName = GetSerializationMethodName(codeProperty.Type, modelParamName);

        if (serializationName == "writeObjectValueFromMethod" || serializationName == "writeCollectionOfObjectValuesFromMethod")
        {
            writer.WriteLine($"writer.{serializationName}(\"{codePropertyName}\", {modelParamName}.{codePropertyName} as any, serialize{propertyTypeName});");
        }
        else
        {
            writer.WriteLine($"writer.{serializationName}(\"{codeProperty.SerializationName ?? codePropertyName}\", {modelParamName}.{codePropertyName});");
        }
        writer.DecreaseIndent();

    }

    private string GetSerializationMethodName(CodeTypeBase propType, string modelParamName)
    {
        var propertyType = localConventions.TranslateType(propType);
        if (propType is CodeType currentType)
        {
            var result = GetSerializationMethodNameForCodeType(currentType, propertyType, modelParamName);
            if (!String.IsNullOrWhiteSpace(result))
            {
                return result;
            }
        }
        return propertyType switch
        {
            "string" or "boolean" or "number" or "Guid" or "Date" or "DateOnly" or "TimeOnly" or "Duration" => $"write{propertyType.ToFirstCharacterUpperCase()}Value",
            _ => $"writeObjectValueFromMethod",
        };
    }

    private static string GetSerializationMethodNameForCodeType(CodeType propType, string propertyType, string modelParamName)
    {
        var isCollection = propType.CollectionKind != CodeTypeBase.CodeTypeCollectionKind.None;
        if (propType.TypeDefinition is CodeEnum currentEnum)
            return $"writeEnumValue<{currentEnum.Name.ToFirstCharacterUpperCase()}>";
        else if (isCollection)
        {
            if (propType.TypeDefinition == null)
                return $"writeCollectionOfPrimitiveValues<{propertyType.ToFirstCharacterLowerCase()}>";
            else
                return $"writeCollectionOfObjectValuesFromMethod";
        }
        return null;
    }

    private void WriteDeserializerMethod(CodeFunction codeElement, LanguageWriter writer)
    {
        var param = codeElement.OriginalLocalMethod.Parameters.FirstOrDefault();

        var codeInterface = (param.Type as CodeType).TypeDefinition as CodeInterface;
        var inherits = codeInterface.StartBlock.Implements.FirstOrDefault(x => x.TypeDefinition is CodeInterface);

        var properties = codeInterface.Properties.Where(static x => x.Kind == CodePropertyKind.Custom && !x.ExistsInBaseType);

        writer.WriteLine("return {");
        writer.IncreaseIndent();
        if (inherits != null)
        {
            writer.WriteLine($"...deserializeInto{inherits.TypeDefinition.Name.ToFirstCharacterUpperCase()}({param.Name.ToFirstCharacterLowerCase()}),");
        }

        foreach (var otherProp in properties)
        {
            writer.WriteLine($"\"{otherProp.SerializationName.ToFirstCharacterLowerCase() ?? otherProp.Name.ToFirstCharacterLowerCase()}\": n => {{ {param.Name.ToFirstCharacterLowerCase()}.{otherProp.Name.ToFirstCharacterLowerCase()} = n.{GetDeserializationMethodName(otherProp.Type)} as any ; }},");
        }

        writer.DecreaseIndent();
        writer.WriteLine("}");

    }

    private string GetDeserializationMethodName(CodeTypeBase propType)
    {
        var isCollection = propType.CollectionKind != CodeTypeBase.CodeTypeCollectionKind.None;
        var propertyType = localConventions.TranslateType(propType);
        if (propType is CodeType currentType)
        {
            if (currentType.TypeDefinition is CodeEnum currentEnum)
                return $"getEnumValue{(currentEnum.Flags || isCollection ? "s" : string.Empty)}<{currentEnum.Name.ToFirstCharacterUpperCase()}>({propertyType.ToFirstCharacterUpperCase()})";
            else if (isCollection)
                if (currentType.TypeDefinition == null)
                    return $"getCollectionOfPrimitiveValues<{propertyType.ToFirstCharacterLowerCase()}>()";
                else
                    return $"getCollectionOfObjectValuesFromMethod(deserializeInto{propertyType.ToFirstCharacterUpperCase()})";
        }
        return propertyType switch
        {
            "string" or "boolean" or "number" or "Guid" or "Date" or "DateOnly" or "TimeOnly" or "Duration" => $"get{propertyType.ToFirstCharacterUpperCase()}Value()",
            _ => $"getObject(deserializeInto{(propType as CodeType).TypeDefinition.Name})",
        };
    }

    private void WriteFactoryMethodBody(CodeFunction codeElement, string returnType, LanguageWriter writer)
    {
        var parseNodeParameter = codeElement.OriginalLocalMethod.Parameters.OfKind(CodeParameterKind.ParseNode);
        if (codeElement.OriginalMethodParentClass.DiscriminatorInformation.ShouldWriteDiscriminatorForInheritedType && parseNodeParameter != null)
        {
            writer.WriteLines($"const mappingValueNode = {parseNodeParameter.Name.ToFirstCharacterLowerCase()}.getChildNode(\"{codeElement.OriginalMethodParentClass.DiscriminatorInformation.DiscriminatorPropertyName}\");",
                                "if (mappingValueNode) {");
            writer.IncreaseIndent();
            writer.WriteLines("const mappingValue = mappingValueNode.getStringValue();",
                            "if (mappingValue) {");
            writer.IncreaseIndent();

            writer.WriteLine("switch (mappingValue) {");
            writer.IncreaseIndent();
            foreach(var mappedType in codeElement.OriginalMethodParentClass.DiscriminatorInformation.DiscriminatorMappings) {
                var typeName = conventions.GetTypeString(mappedType.Value, codeElement, false, writer);
                writer.WriteLine($"case \"{mappedType.Key}\":");
                writer.IncreaseIndent();
                writer.WriteLine($"return new {typeName.ToFirstCharacterUpperCase()}();");
                writer.DecreaseIndent();
            }
            writer.CloseBlock();
            writer.CloseBlock();
            writer.CloseBlock();
        }

        writer.WriteLine($"return new {returnType.ToFirstCharacterUpperCase()}();");
    }
}
