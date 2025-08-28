using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceNugetPackageBuilder
{
    public class MakeInternalRewriter : CSharpSyntaxRewriter
    {
        public static bool TryProcess(string sourceCode, out string internalSourceCode)
        {
            // Usage
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = (CompilationUnitSyntax)tree.GetRoot();            

            var rewriter = new MakeInternalRewriter();
            var newRoot = rewriter.Visit(root);

            internalSourceCode = newRoot.NormalizeWhitespace().ToFullString();

            return rewriter._ChangesApplied;
        }

        private bool _ChangesApplied = false;

        public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            if (!_TryMakeModifiersInternal(node, out var newModifiers)) return node;

            _ChangesApplied = true;

            // Produce the new class declaration with the internal modifier
            var newNode = node.WithModifiers(SyntaxFactory.TokenList(newModifiers));

            return base.VisitEnumDeclaration(newNode);
        }

        public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        {
            if (!_TryMakeModifiersInternal(node, out var newModifiers)) return node;

            _ChangesApplied = true;

            // Produce the new class declaration with the internal modifier
            var newNode = node.WithModifiers(SyntaxFactory.TokenList(newModifiers));

            return base.VisitStructDeclaration(newNode);
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (!_TryMakeModifiersInternal(node, out var newModifiers)) return node;

            _ChangesApplied = true;

            // Produce the new class declaration with the internal modifier
            var newNode = node.WithModifiers(SyntaxFactory.TokenList(newModifiers));

            return base.VisitClassDeclaration(newNode);
        }

        private static bool _TryMakeModifiersInternal(BaseTypeDeclarationSyntax node, out List<SyntaxToken> newModifiers)
        {
            newModifiers = null;

            // Check if the class is already internal
            if (node.Modifiers.Any(SyntaxKind.InternalKeyword)) return false;

            // Remove the public modifier if it exists
            newModifiers = node.Modifiers
                .Where(m => !m.IsKind(SyntaxKind.PublicKeyword))
                .ToList();

            // Add the internal modifier
            newModifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.InternalKeyword));

            return true;
        }
    }


    /// <summary>
    /// Validates source code files to ensure they're ready for source code packaging
    /// </summary>
    public static class SourceCodeValidator
    {
        public static string RequieredUsingDirectivesPrefix = "_";

        public static Exception Validate(string sourceCode)
        {
            if (sourceCode == null) return new ArgumentNullException(nameof(sourceCode));

            if (!sourceCode.Contains("#nullable disable"))
            {
                return new InvalidOperationException("must include #nullable disable at the beginning of the file");               
            }

            if (!string.IsNullOrWhiteSpace(RequieredUsingDirectivesPrefix))
            {
                if (UsingDirectivesExtensions.GetUsingDirectives(sourceCode).Any(item => !item.Key.StartsWith(RequieredUsingDirectivesPrefix)))
                {
                    return new InvalidOperationException("using directives must begin with double underscore __ to prevent collisions with global usings");
                }                
            }

            return null;
        }
    }

    static class NullableContextExtensions
    {
        public static bool IsInNullableContext(this CSharpSyntaxNode node, SemanticModel semanticModel)
        {
            // CSharpCompilation compilation = ...; // your CSharpCompilation object
            // SyntaxTree syntaxTree = ...; // your SyntaxTree object
            // SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

            var location = node.GetLocation();

            var nullableContext = semanticModel.GetNullableContext(location.SourceSpan.Start);

            return AnnotationsEnabled(nullableContext);
        }        

        public static bool WarningsEnabled(this NullableContext context) => IsFlagSet(context, NullableContext.WarningsEnabled);

        public static bool AnnotationsEnabled(this NullableContext context) => IsFlagSet(context, NullableContext.AnnotationsEnabled);

        private static bool IsFlagSet(this NullableContext context, NullableContext flag) => (context & flag) == flag;
    }

    static class UsingDirectivesExtensions
    {
        public static IEnumerable<KeyValuePair<string,string>> GetUsingDirectives(string sourceCode)
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetCompilationUnitRoot();

            foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                if (usingDirective.Alias != null)
                {
                    yield return new KeyValuePair<string, string>(usingDirective.Alias.Name.ToString(), usingDirective.Name.ToString());                    
                }
            }
        }
    }
}
