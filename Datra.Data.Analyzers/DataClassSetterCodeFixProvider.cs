using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Datra.Data.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DataClassSetterCodeFixProvider)), Shared]
    public class DataClassSetterCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Remove setter";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DataClassSetterAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the setter accessor
            var setter = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf()
                .OfType<AccessorDeclarationSyntax>()
                .FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));

            if (setter == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => RemoveSetterAsync(context.Document, setter, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Document> RemoveSetterAsync(
            Document document,
            AccessorDeclarationSyntax setter,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var property = setter.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
            
            if (property == null)
                return document;

            var accessorList = property.AccessorList;
            if (accessorList == null)
                return document;

            // Remove the setter from the accessor list
            var newAccessors = accessorList.Accessors.Remove(setter);
            
            PropertyDeclarationSyntax newProperty;
            
            if (newAccessors.Count == 0)
            {
                // If no accessors left, convert to expression-bodied property or auto-property with getter only
                if (property.ExpressionBody != null)
                {
                    // Already expression-bodied, just remove the accessor list
                    newProperty = property.WithAccessorList(null);
                }
                else
                {
                    // Convert to auto-property with getter only
                    newProperty = property.WithAccessorList(
                        SyntaxFactory.AccessorList(
                            SyntaxFactory.SingletonList(
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))));
                }
            }
            else if (newAccessors.Count == 1 && newAccessors[0].IsKind(SyntaxKind.GetAccessorDeclaration))
            {
                // Only getter left
                var getter = newAccessors[0];
                
                // If it's an auto-property getter, keep it as auto-property
                if (getter.Body == null && getter.ExpressionBody == null)
                {
                    newProperty = property.WithAccessorList(
                        SyntaxFactory.AccessorList(
                            SyntaxFactory.SingletonList(getter)));
                }
                else
                {
                    // Keep the accessor list with just the getter
                    newProperty = property.WithAccessorList(
                        accessorList.WithAccessors(newAccessors));
                }
            }
            else
            {
                // Multiple accessors remaining, just remove the setter
                newProperty = property.WithAccessorList(
                    accessorList.WithAccessors(newAccessors));
            }

            var newRoot = root.ReplaceNode(property, newProperty);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}