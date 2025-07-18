using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Datra.Data.Generators.Models;

namespace Datra.Data.Generators.Analyzers
{
    internal class DataModelAnalyzer
    {
        private readonly Compilation _compilation;
        private readonly INamedTypeSymbol _tableDataAttrSymbol;
        private readonly INamedTypeSymbol _singleDataAttrSymbol;

        public DataModelAnalyzer(Compilation compilation)
        {
            _compilation = compilation;
            _tableDataAttrSymbol = compilation.GetTypeByMetadataName("Datra.Data.Attributes.TableDataAttribute");
            _singleDataAttrSymbol = compilation.GetTypeByMetadataName("Datra.Data.Attributes.SingleDataAttribute");
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
                formatValue = formatArg.Value.Value.ToString();
            }

            var modelInfo = new DataModelInfo
            {
                TypeName = classSymbol.ToDisplayString(),
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
                    iface.ConstructedFrom.ToDisplayString() == "Datra.Data.Interfaces.ITableData<TKey>")
                {
                    return iface.TypeArguments[0].ToDisplayString();
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
                    properties.Add(new PropertyInfo
                    {
                        Name = property.Name,
                        Type = property.Type.ToDisplayString(),
                        IsNullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated
                    });
                }
            }

            GeneratorLogger.Log($"Found {properties.Count} properties in {classSymbol.Name}");
            return properties;
        }
    }
}