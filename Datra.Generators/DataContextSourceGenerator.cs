using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Datra.Generators.Analyzers;
using Datra.Generators.Builders;
using Datra.Generators.Generators;
using Datra.Generators.SyntaxReceivers;

namespace Datra.Generators
{
    [Generator]
    public class DataContextSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new DataAttributeSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            GeneratorLogger.StartLogging();
            GeneratorLogger.Log("Starting source generation");
            
            try
            {
                if (!(context.SyntaxReceiver is DataAttributeSyntaxReceiver receiver))
                {
                    GeneratorLogger.LogError("SyntaxReceiver is not DataAttributeSyntaxReceiver");
                    GeneratorLogger.AddDebugOutput(context);
                    return;
                }

                var compilation = context.Compilation;
                var analyzer = new DataModelAnalyzer(compilation);
                
                if (!analyzer.IsInitialized)
                {
                    GeneratorLogger.LogError("Required attributes not found in compilation");
                    GeneratorLogger.AddDebugOutput(context);
                    return;
                }

                // Analyze candidate classes
                GeneratorLogger.Log($"Found {receiver.CandidateClasses.Count} candidate classes");
                
                // Log all candidate classes for debugging
                foreach (var candidateClass in receiver.CandidateClasses)
                {
                    var classNamespace = (candidateClass.Parent as NamespaceDeclarationSyntax)?.Name.ToString() ?? "Unknown";
                    GeneratorLogger.Log($"  - Class: {candidateClass.Identifier.Text} in namespace: {classNamespace}");
                }
                
                var dataModels = analyzer.AnalyzeClasses(receiver.CandidateClasses);

            if (dataModels.Count == 0)
            {
                GeneratorLogger.Log("No data models found");
                GeneratorLogger.Log("Candidate classes were:");
                foreach (var candidateClass in receiver.CandidateClasses)
                {
                    GeneratorLogger.Log($"  - {candidateClass.Identifier.Text}");
                }
                GeneratorLogger.AddDebugOutput(context);
                return;
            }
            
            GeneratorLogger.Log($"Successfully analyzed {dataModels.Count} data models");

            // Use a dedicated namespace for the generated DataContext
            // This avoids issues when model classes are spread across multiple namespaces
            var namespaceName = "Datra.Generated";
            GeneratorLogger.Log($"Using namespace: {namespaceName}");
            
            // Get configuration from assembly attribute
            string localizationKeysPath = "Localizations/LocalizationKeys.csv"; // default
            string contextName = "GameDataContext"; // default
            string generatedNamespace = namespaceName; // use default
            
            var attributes = compilation.Assembly.GetAttributes();
            var configAttr = attributes.FirstOrDefault(a => 
                a.AttributeClass?.Name == "DatraConfigurationAttribute" ||
                a.AttributeClass?.ToDisplayString() == "Datra.Attributes.DatraConfigurationAttribute");
            
            if (configAttr != null)
            {
                foreach (var arg in configAttr.NamedArguments)
                {
                    switch (arg.Key)
                    {
                        case "LocalizationKeyDataPath":
                            localizationKeysPath = arg.Value.Value?.ToString() ?? localizationKeysPath;
                            break;
                        case "DataContextName":
                            contextName = arg.Value.Value?.ToString() ?? contextName;
                            break;
                        case "GeneratedNamespace":
                            generatedNamespace = arg.Value.Value?.ToString() ?? generatedNamespace;
                            break;
                    }
                }
                GeneratorLogger.Log($"Found DatraConfigurationAttribute: LocalizationKeyDataPath={localizationKeysPath}, DataContextName={contextName}, GeneratedNamespace={generatedNamespace}");
            }

            // Filter out localization models
            var filteredModels = dataModels
                .Where(m => !m.TypeName.Contains("LocalizationKeyData") && !m.TypeName.Contains("LocalizationData"))
                .ToList();
            
            GeneratorLogger.Log($"Filtered out localization models. {filteredModels.Count} models remaining.");

            // Generate DataContext
            var dataContextGenerator = new DataContextGenerator();
            var sourceCode = dataContextGenerator.GenerateDataContext(generatedNamespace, contextName, filteredModels, localizationKeysPath);
            context.AddSource($"{contextName}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
            GeneratorLogger.Log($"Generated {contextName}.g.cs");

            // Generate DataModel files
            var dataModelGenerator = new DataModelGenerator(context);
            foreach (var model in dataModels)
            {
                var dataModelCode = dataModelGenerator.GenerateDataModelFile(model);
                var fileName = $"{CodeBuilder.GetSimpleTypeName(model.TypeName)}.g.cs";
                context.AddSource(fileName, SourceText.From(dataModelCode, Encoding.UTF8));
                GeneratorLogger.Log($"Generated {fileName}");
            }
            
            // Generate LocalizationKeyDataSerializer
            var localizationSerializerCode = LocalizationKeyDataSerializer.GenerateSerializer();
            context.AddSource("LocalizationKeyDataSerializer.g.cs", SourceText.From(localizationSerializerCode, Encoding.UTF8));
            GeneratorLogger.Log("Generated LocalizationKeyDataSerializer.g.cs");
            
                GeneratorLogger.Log("DataContextSourceGenerator execution completed");
            }
            catch (Exception ex)
            {
                GeneratorLogger.LogError($"Unexpected error in source generator: {ex.Message}", ex);
                GeneratorLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                GeneratorLogger.AddDebugOutput(context);
            }
        }
    }
}