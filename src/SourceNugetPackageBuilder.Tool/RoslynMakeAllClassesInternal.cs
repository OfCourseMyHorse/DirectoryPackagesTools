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
}
