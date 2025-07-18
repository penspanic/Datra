using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datra.Data.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DataClassSetterAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DATRA001";
        private const string Category = "Design";

        private static readonly LocalizableString Title = "Data class property should not have setter";
        private static readonly LocalizableString MessageFormat = "Property '{0}' in data class '{1}' should not have a setter";
        private static readonly LocalizableString Description = "Data classes should be immutable. Remove the setter and use constructor or init-only properties instead.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        }

        private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
            
            // Check if property has a setter
            var setter = propertyDeclaration.AccessorList?.Accessors
                .FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
            
            if (setter == null)
                return;

            // Get the containing class
            var classDeclaration = propertyDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (classDeclaration == null)
                return;

            var semanticModel = context.SemanticModel;
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            if (classSymbol == null)
                return;

            // Check if this is a data class
            if (!IsDataClass(classSymbol))
                return;

            // Report diagnostic
            var diagnostic = Diagnostic.Create(
                Rule,
                setter.GetLocation(),
                propertyDeclaration.Identifier.Text,
                classSymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsDataClass(INamedTypeSymbol classSymbol)
        {
            // Check for TableData or SingleData attributes
            var hasDataAttribute = classSymbol.GetAttributes().Any(attr =>
            {
                var attrClass = attr.AttributeClass;
                if (attrClass == null)
                    return false;

                var fullName = attrClass.ToDisplayString();
                return fullName == "Datra.Data.Attributes.TableDataAttribute" ||
                       fullName == "Datra.Data.Attributes.SingleDataAttribute";
            });

            if (hasDataAttribute)
                return true;

            // Check if implements ITableData<T>
            return classSymbol.AllInterfaces.Any(i =>
                i.IsGenericType &&
                i.ConstructedFrom.ToDisplayString() == "Datra.Data.Interfaces.ITableData<T>");
        }
    }
}