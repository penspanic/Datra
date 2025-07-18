using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Datra.Data.Generators.Analyzers;
using Datra.Data.Generators.Builders;
using Datra.Data.Generators.Generators;
using Datra.Data.Generators.SyntaxReceivers;

namespace Datra.Data.Generators
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
            var dataModels = analyzer.AnalyzeClasses(receiver.CandidateClasses);

            if (dataModels.Count == 0)
            {
                GeneratorLogger.Log("No data models found");
                GeneratorLogger.AddDebugOutput(context);
                return;
            }

            // Extract namespace from first class
            var firstClass = receiver.CandidateClasses.First();
            var namespaceDeclaration = firstClass.Parent as NamespaceDeclarationSyntax ??
                                       firstClass.Parent?.Parent as NamespaceDeclarationSyntax;
            var namespaceName = namespaceDeclaration?.Name.ToString() ?? "Generated";
            GeneratorLogger.Log($"Using namespace: {namespaceName}");

            // Generate DataContext
            var dataContextGenerator = new DataContextGenerator();
            var sourceCode = dataContextGenerator.GenerateDataContext(namespaceName, "GameDataContext", dataModels);
            context.AddSource("GameDataContext.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
            GeneratorLogger.Log("Generated GameDataContext.g.cs");

            // Generate Serializer files
            var serializerGenerator = new SerializerGenerator(context);
            foreach (var model in dataModels)
            {
                var serializerCode = serializerGenerator.GenerateSerializerFile(model);
                var fileName = $"{CodeBuilder.GetSimpleTypeName(model.TypeName)}.g.cs";
                context.AddSource(fileName, SourceText.From(serializerCode, Encoding.UTF8));
                GeneratorLogger.Log($"Generated {fileName}");
            }
            
            GeneratorLogger.Log("DataContextSourceGenerator execution completed");
            GeneratorLogger.AddDebugOutput(context);
        }
    }
}