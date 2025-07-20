using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Datra.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DataClassSetterUsageAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DATRA001";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "Data class property setter usage detected";
        private static readonly LocalizableString MessageFormat = "Cannot set property '{0}' on data class '{1}'. Data classes are immutable.";
        private static readonly LocalizableString Description = "Data classes should be immutable. Setting properties on data class instances is not allowed.";

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
            context.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment);
            context.RegisterOperationAction(AnalyzeCompoundAssignment, OperationKind.CompoundAssignment);
        }

        private static void AnalyzeAssignment(OperationAnalysisContext context)
        {
            var assignmentOperation = (ISimpleAssignmentOperation)context.Operation;
            AnalyzePropertyAssignment(context, assignmentOperation.Target);
        }

        private static void AnalyzeCompoundAssignment(OperationAnalysisContext context)
        {
            var assignmentOperation = (ICompoundAssignmentOperation)context.Operation;
            AnalyzePropertyAssignment(context, assignmentOperation.Target);
        }

        private static void AnalyzePropertyAssignment(OperationAnalysisContext context, IOperation target)
        {
            if (target is not IPropertyReferenceOperation propertyReference)
                return;

            var property = propertyReference.Property;
            var containingType = property.ContainingType;

            if (containingType == null || !IsDataClass(containingType))
                return;

            // Skip if this is inside a constructor or init accessor
            var syntaxNode = context.Operation.Syntax;
            
            // Check if we're in a constructor
            var containingConstructor = syntaxNode.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
            if (containingConstructor != null)
            {
                // Allow assignments in constructors of the same class
                var constructorSymbol = context.ContainingSymbol as IMethodSymbol;
                if (constructorSymbol?.ContainingType?.Equals(containingType, SymbolEqualityComparer.Default) == true)
                    return;
            }
            
            // Check if we're in an init accessor
            var containingAccessor = syntaxNode.FirstAncestorOrSelf<AccessorDeclarationSyntax>();
            if (containingAccessor != null && containingAccessor.IsKind(SyntaxKind.InitAccessorDeclaration))
                return;

            // Report diagnostic
            var diagnostic = Diagnostic.Create(
                Rule,
                target.Syntax.GetLocation(),
                property.Name,
                containingType.Name);

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
                return fullName == "Datra.Attributes.TableDataAttribute" ||
                       fullName == "Datra.Attributes.SingleDataAttribute";
            });

            if (hasDataAttribute)
                return true;

            // Check if implements ITableData<T>
            return classSymbol.AllInterfaces.Any(i =>
                i.IsGenericType &&
                i.ConstructedFrom.ToDisplayString() == "Datra.Interfaces.ITableData<T>");
        }
    }
}