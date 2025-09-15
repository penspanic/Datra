using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Datra.Generators.Models;

namespace Datra.Generators.Analyzers
{
    internal class DataModelAnalyzer
    {
        private readonly Compilation _compilation;
        private readonly INamedTypeSymbol _tableDataAttrSymbol;
        private readonly INamedTypeSymbol _singleDataAttrSymbol;
        
        // Define a SymbolDisplayFormat that includes global:: prefix
        private static readonly SymbolDisplayFormat FullyQualifiedFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.None,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );

        public DataModelAnalyzer(Compilation compilation)
        {
            _compilation = compilation;
            _tableDataAttrSymbol = compilation.GetTypeByMetadataName("Datra.Attributes.TableDataAttribute");
            _singleDataAttrSymbol = compilation.GetTypeByMetadataName("Datra.Attributes.SingleDataAttribute");
        }

        public bool IsInitialized => _tableDataAttrSymbol != null && _singleDataAttrSymbol != null;

        public List<DataModelInfo> AnalyzeClasses(List<ClassDeclarationSyntax> candidateClasses)
        {
            var dataModels = new List<DataModelInfo>();

            foreach (var classDeclaration in candidateClasses)
            {
                GeneratorLogger.Log($"Analyzing class: {classDeclaration.Identifier.Text}");
                
                var model = _compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = model.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

                if (classSymbol == null)
                {
                    GeneratorLogger.LogWarning($"Could not get symbol for class: {classDeclaration.Identifier.Text}");
                    continue;
                }

                var dataModel = AnalyzeClass(classSymbol);
                if (dataModel != null)
                {
                    dataModels.Add(dataModel);
                    GeneratorLogger.Log($"Found data model: {dataModel.TypeName} ({(dataModel.IsTableData ? "Table" : "Single")})");
                }
            }

            return dataModels;
        }

        private DataModelInfo AnalyzeClass(INamedTypeSymbol classSymbol)
        {
            var attributes = classSymbol.GetAttributes();
            AttributeData dataAttribute = null;
            bool isTableData = false;

            foreach (var attr in attributes)
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, _tableDataAttrSymbol))
                {
                    dataAttribute = attr;
                    isTableData = true;
                    break;
                }
                else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, _singleDataAttrSymbol))
                {
                    dataAttribute = attr;
                    isTableData = false;
                    break;
                }
            }

            if (dataAttribute == null)
                return null;

            var filePath = dataAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
            if (string.IsNullOrEmpty(filePath))
            {
                GeneratorLogger.LogWarning($"No file path specified for {classSymbol.Name}");
                return null;
            }

            var formatValue = "Auto";
            var formatArg = dataAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "Format");
            if (formatArg.Value.Value != null)
            {
                // Get the actual enum field
                if (formatArg.Value.Kind == TypedConstantKind.Enum)
                {
                    // Convert enum value to its name
                    var enumValue = Convert.ToInt32(formatArg.Value.Value);
                    var enumType = formatArg.Value.Type;
                    
                    // Get enum member names
                    var members = enumType.GetMembers().OfType<IFieldSymbol>()
                        .Where(f => f.IsConst && f.ConstantValue != null);
                    
                    foreach (var member in members)
                    {
                        if (Convert.ToInt32(member.ConstantValue) == enumValue)
                        {
                            formatValue = member.Name;
                            break;
                        }
                    }
                }
                else
                {
                    formatValue = formatArg.Value.Value.ToString();
                }
                
                GeneratorLogger.Log($"Format value for {classSymbol.Name}: '{formatValue}'");
            }

            var modelInfo = new DataModelInfo
            {
                TypeName = classSymbol.ToDisplayString(FullyQualifiedFormat),
                PropertyName = classSymbol.Name.Replace("Data", ""),
                IsTableData = isTableData,
                FilePath = filePath,
                Format = formatValue,
                Properties = GetProperties(classSymbol)
            };

            if (isTableData)
            {
                modelInfo.KeyType = GetKeyType(classSymbol);
                if (string.IsNullOrEmpty(modelInfo.KeyType))
                {
                    GeneratorLogger.LogWarning($"Could not determine key type for table data: {classSymbol.Name}");
                }
            }

            return modelInfo;
        }

        private string GetKeyType(INamedTypeSymbol classSymbol)
        {
            foreach (var iface in classSymbol.AllInterfaces)
            {
                if (iface.IsGenericType && 
                    iface.ConstructedFrom.ToDisplayString(FullyQualifiedFormat) == "global::Datra.Interfaces.ITableData<TKey>")
                {
                    return iface.TypeArguments[0].ToDisplayString(FullyQualifiedFormat);
                }
            }
            return null;
        }

        private List<PropertyInfo> GetProperties(INamedTypeSymbol classSymbol)
        {
            var properties = new List<PropertyInfo>();

            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IPropertySymbol property && property.DeclaredAccessibility == Accessibility.Public)
                {
                    var propertyType = property.Type;
                    var isDataRef = false;
                    string dataRefKeyType = null;
                    string dataRefTargetType = null;
                    var isArray = false;
                    string elementType = null;
                    
                    // Enhanced type metadata
                    bool isEnum = false;
                    bool isValueType = false;
                    bool elementIsEnum = false;
                    bool elementIsValueType = false;
                    string cleanTypeName = null;
                    string cleanElementType = null;

                    // Check if it's an array type
                    if (propertyType is IArrayTypeSymbol arrayType)
                    {
                        isArray = true;
                        var elementTypeSymbol = arrayType.ElementType;
                        elementType = elementTypeSymbol.ToDisplayString(FullyQualifiedFormat);
                        cleanElementType = elementTypeSymbol.ToDisplayString();

                        // Capture element type metadata
                        elementIsEnum = elementTypeSymbol.TypeKind == TypeKind.Enum;
                        elementIsValueType = elementTypeSymbol.IsValueType;

                        // Check if array element is DataRef
                        if (elementTypeSymbol is INamedTypeSymbol namedElementType &&
                            namedElementType.IsGenericType)
                        {
                            var constructedFrom = namedElementType.ConstructedFrom.ToDisplayString(FullyQualifiedFormat);
                            if (constructedFrom == "global::Datra.DataTypes.StringDataRef<T>" ||
                                constructedFrom == "global::Datra.DataTypes.IntDataRef<T>")
                            {
                                isDataRef = true;
                                dataRefKeyType = constructedFrom.Contains("StringDataRef") ? "string" : "int";
                                dataRefTargetType = namedElementType.TypeArguments[0].Name;
                            }
                        }
                    }
                    // Check if it's any DataRef type (non-array)
                    else if (propertyType is INamedTypeSymbol namedType &&
                        namedType.IsGenericType)
                    {
                        var constructedFrom = namedType.ConstructedFrom.ToDisplayString(FullyQualifiedFormat);
                        if (constructedFrom == "global::Datra.DataTypes.StringDataRef<T>" ||
                            constructedFrom == "global::Datra.DataTypes.IntDataRef<T>")
                        {
                            isDataRef = true;
                            dataRefKeyType = constructedFrom.Contains("StringDataRef") ? "string" : "int";
                            dataRefTargetType = namedType.TypeArguments[0].Name;
                        }
                    }

                    // Capture main type metadata
                    cleanTypeName = property.Type.ToDisplayString();
                    isEnum = propertyType.TypeKind == TypeKind.Enum;
                    isValueType = propertyType.IsValueType;

                    properties.Add(new PropertyInfo
                    {
                        Name = property.Name,
                        Type = property.Type.ToDisplayString(FullyQualifiedFormat),
                        IsNullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated,
                        IsDataRef = isDataRef,
                        DataRefKeyType = dataRefKeyType,
                        DataRefTargetType = dataRefTargetType,
                        IsArray = isArray,
                        ElementType = elementType,
                        // New enhanced metadata
                        CleanTypeName = cleanTypeName,
                        CleanElementType = cleanElementType,
                        IsEnum = isEnum,
                        IsValueType = isValueType,
                        ElementIsEnum = elementIsEnum,
                        ElementIsValueType = elementIsValueType
                    });
                }
            }

            GeneratorLogger.Log($"Found {properties.Count} properties in {classSymbol.Name}");
            return properties;
        }
    }
}