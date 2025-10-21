using System;
using System.IO;
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
            string localizationDataPath = "Localizations/"; // default
            string defaultLanguage = "en"; // default (ISO 639-1 code)
            string contextName = "GameDataContext"; // default
            string generatedNamespace = namespaceName; // use default
            bool enableLocalization = false; // default
            bool enableDebugLogging = false; // default
            bool emitPhysicalFiles = false; // default
            string physicalFilesPath = null; // default

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
                        case "EnableLocalization":
                            enableLocalization = arg.Value.Value is bool b ? b : false;
                            break;
                        case "LocalizationKeyDataPath":
                            localizationKeysPath = arg.Value.Value?.ToString() ?? localizationKeysPath;
                            break;
                        case "LocalizationDataPath":
                            localizationDataPath = arg.Value.Value?.ToString() ?? localizationDataPath;
                            break;
                        case "DefaultLanguage":
                            defaultLanguage = arg.Value.Value?.ToString() ?? defaultLanguage;
                            break;
                        case "DataContextName":
                            contextName = arg.Value.Value?.ToString() ?? contextName;
                            break;
                        case "GeneratedNamespace":
                            generatedNamespace = arg.Value.Value?.ToString() ?? generatedNamespace;
                            break;
                        case "EnableDebugLogging":
                            enableDebugLogging = arg.Value.Value is bool debug ? debug : false;
                            break;
                        case "EmitPhysicalFiles":
                            emitPhysicalFiles = arg.Value.Value is bool emit ? emit : false;
                            break;
                        case "PhysicalFilesPath":
                            physicalFilesPath = arg.Value.Value?.ToString();
                            break;
                    }
                }
                GeneratorLogger.Log($"Found DatraConfigurationAttribute: EnableLocalization={enableLocalization}, LocalizationKeyDataPath={localizationKeysPath}, LocalizationDataPath={localizationDataPath}, DefaultLanguage={defaultLanguage}, DataContextName={contextName}, GeneratedNamespace={generatedNamespace}, EnableDebugLogging={enableDebugLogging}, EmitPhysicalFiles={emitPhysicalFiles}, PhysicalFilesPath={physicalFilesPath}");
            }

            // Filter out localization models
            var filteredModels = dataModels
                .Where(m => !m.TypeName.Contains("LocalizationKeyData") && !m.TypeName.Contains("LocalizationData"))
                .ToList();
            
            GeneratorLogger.Log($"Filtered out localization models. {filteredModels.Count} models remaining.");

            // Generate DataContext
            var dataContextGenerator = new DataContextGenerator();
            var sourceCode = dataContextGenerator.GenerateDataContext(generatedNamespace, contextName, filteredModels,
                localizationKeysPath, localizationDataPath, defaultLanguage, enableLocalization, enableDebugLogging);
            context.AddSource($"{contextName}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
            GeneratorLogger.Log($"Generated {contextName}.g.cs");

            // Emit physical file for DataContext if enabled
            if (emitPhysicalFiles)
            {
                EmitPhysicalFile(context, dataModels, $"{contextName}.g.cs", sourceCode, physicalFilesPath, isContext: true);
            }

            // Generate DataModel files
            var dataModelGenerator = new DataModelGenerator(context);
            foreach (var model in dataModels)
            {
                var dataModelCode = dataModelGenerator.GenerateDataModelFile(model);
                var fileName = $"{CodeBuilder.GetSimpleTypeName(model.TypeName)}.g.cs";
                context.AddSource(fileName, SourceText.From(dataModelCode, Encoding.UTF8));
                GeneratorLogger.Log($"Generated {fileName}");

                // Emit physical file for DataModel if enabled
                if (emitPhysicalFiles)
                {
                    EmitPhysicalFileForModel(model, fileName, dataModelCode, physicalFilesPath);
                }
            }
            
            // Generate LocalizationKeyDataSerializer only if localization is enabled
            if (enableLocalization)
            {
                var localizationSerializerCode = LocalizationKeyDataSerializer.GenerateSerializer();
                context.AddSource("LocalizationKeyDataSerializer.g.cs", SourceText.From(localizationSerializerCode, Encoding.UTF8));
                GeneratorLogger.Log("Generated LocalizationKeyDataSerializer.g.cs");
            }
            
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

        /// <summary>
        /// Emit a physical file for the DataContext to the project directory
        /// </summary>
        private static void EmitPhysicalFile(GeneratorExecutionContext context, System.Collections.Generic.List<Models.DataModelInfo> dataModels, string fileName, string content, string physicalFilesPath, bool isContext)
        {
            try
            {
                string projectDir = GetProjectDirectory(context, dataModels);
                if (string.IsNullOrEmpty(projectDir))
                {
                    GeneratorLogger.LogWarning("Could not determine project directory for physical file emission");
                    return;
                }

                string targetPath;
                if (string.IsNullOrEmpty(physicalFilesPath))
                {
                    // Generate in project root for context
                    targetPath = Path.Combine(projectDir, fileName);
                }
                else
                {
                    // Generate in specified folder
                    var targetDir = Path.Combine(projectDir, physicalFilesPath);
                    Directory.CreateDirectory(targetDir);
                    targetPath = Path.Combine(targetDir, fileName);
                }

                WriteFileIfChanged(targetPath, content);
                GeneratorLogger.Log($"Emitted physical file: {targetPath}");
            }
            catch (Exception ex)
            {
                GeneratorLogger.LogError($"Failed to emit physical file {fileName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Emit a physical file for a DataModel next to its source file or in a specified folder
        /// </summary>
        private static void EmitPhysicalFileForModel(Models.DataModelInfo model, string fileName, string content, string physicalFilesPath)
        {
            try
            {
                string targetPath;
                if (string.IsNullOrEmpty(physicalFilesPath))
                {
                    // Generate next to source file
                    if (string.IsNullOrEmpty(model.SourceFilePath))
                    {
                        GeneratorLogger.LogWarning($"No source file path for model {model.TypeName}, cannot emit physical file");
                        return;
                    }

                    var sourceDir = Path.GetDirectoryName(model.SourceFilePath);
                    targetPath = Path.Combine(sourceDir, fileName);
                }
                else
                {
                    // Generate in specified folder
                    var projectDir = FindProjectRoot(model.SourceFilePath);
                    if (string.IsNullOrEmpty(projectDir))
                    {
                        GeneratorLogger.LogWarning($"Could not find project root for {model.SourceFilePath}");
                        return;
                    }

                    var targetDir = Path.Combine(projectDir, physicalFilesPath);
                    Directory.CreateDirectory(targetDir);
                    targetPath = Path.Combine(targetDir, fileName);
                }

                WriteFileIfChanged(targetPath, content);
                GeneratorLogger.Log($"Emitted physical file: {targetPath}");
            }
            catch (Exception ex)
            {
                GeneratorLogger.LogError($"Failed to emit physical file {fileName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get the project directory from MSBuild properties or by finding the .csproj file
        /// </summary>
        private static string GetProjectDirectory(GeneratorExecutionContext context, System.Collections.Generic.List<Models.DataModelInfo> dataModels)
        {
            // Try to get from MSBuild properties
            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir))
            {
                return projectDir;
            }

            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out projectDir))
            {
                return projectDir;
            }

            // Fallback: find from first model's source file
            if (dataModels.Count > 0 && !string.IsNullOrEmpty(dataModels[0].SourceFilePath))
            {
                return FindProjectRoot(dataModels[0].SourceFilePath);
            }

            return null;
        }

        /// <summary>
        /// Find the project root by looking for a .csproj file in parent directories
        /// </summary>
        private static string FindProjectRoot(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.GetFiles(dir, "*.csproj").Length > 0)
                    return dir;

                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }

        /// <summary>
        /// Write file only if the content has changed (to avoid triggering unnecessary rebuilds)
        /// </summary>
        private static void WriteFileIfChanged(string filePath, string content)
        {
            bool shouldWrite = true;

            if (File.Exists(filePath))
            {
                var existingContent = File.ReadAllText(filePath);
                if (existingContent == content)
                {
                    shouldWrite = false;
                    GeneratorLogger.Log($"File unchanged, skipping write: {filePath}");
                }
            }

            if (shouldWrite)
            {
                File.WriteAllText(filePath, content);
                GeneratorLogger.Log($"Wrote file: {filePath}");
            }
        }
    }
}