using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ronimizy.Razor.Sdk.ModifierTool.SyntaxRewriters;

public class AccessModifierSyntaxRewriter : CSharpSyntaxRewriter
{
    private readonly IReadOnlyCollection<string> _excludedFromOpenNamespaces;
    private readonly SemanticModel _semanticModel;

    public AccessModifierSyntaxRewriter(
        IReadOnlyCollection<string> excludedFromOpenNamespaces,
        SemanticModel semanticModel)
    {
        _excludedFromOpenNamespaces = excludedFromOpenNamespaces;
        _semanticModel = semanticModel;
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (ShouldIgnore(node) is false && TryOpenModifiers(
                node.Modifiers,
                out SyntaxTokenList resultModifiers,
                replaceProtected: true,
                replaceEmpty: true))
        {
            node = node.WithModifiers(resultModifiers);
        }

        return base.VisitClassDeclaration(node);
    }

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (ShouldIgnore(node) is false && TryOpenModifiers(
                node.Modifiers,
                out SyntaxTokenList resultModifiers,
                replaceProtected: true,
                replaceEmpty: true))
        {
            node = node.WithModifiers(resultModifiers);
        }

        return base.VisitStructDeclaration(node);
    }

    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        if (ShouldIgnore(node) is false && TryOpenModifiers(
                node.Modifiers,
                out SyntaxTokenList resultModifiers,
                replaceProtected: true,
                replaceEmpty: true))
        {
            node = node.WithModifiers(resultModifiers);
        }

        return base.VisitRecordDeclaration(node);
    }

    public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        if (ShouldIgnore(node) is false && TryOpenModifiers(
                node.Modifiers,
                out SyntaxTokenList resultModifiers,
                replaceProtected: true,
                replaceEmpty: true))
        {
            node = node.WithModifiers(resultModifiers);
        }

        return base.VisitInterfaceDeclaration(node);
    }

    public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        if (ShouldIgnore(node) is false && TryOpenModifiers(
                node.Modifiers,
                out SyntaxTokenList resultModifiers,
                replaceProtected: true,
                replaceEmpty: true))
        {
            node = node.WithModifiers(resultModifiers);
        }

        return base.VisitEnumDeclaration(node);
    }

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        if (node.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ConstKeyword)) is false)
            return base.VisitFieldDeclaration(node);

        if (TryOpenModifiers(node.Modifiers, out SyntaxTokenList resultModifiers))
        {
            node = node.WithModifiers(resultModifiers);
        }

        return base.VisitFieldDeclaration(node);
    }

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        if (TryOpenModifiers(node.Modifiers, out SyntaxTokenList resultModifiers))
        {
            node = node.WithModifiers(resultModifiers);
        }

        return base.VisitConstructorDeclaration(node);
    }

    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (TryOpenModifiers(node.Modifiers, out SyntaxTokenList resultModifiers))
        {
            node = node.WithModifiers(resultModifiers);
        }

        return base.VisitPropertyDeclaration(node);
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (TryOpenModifiers(node.Modifiers, out SyntaxTokenList resultModifiers))
        {
            node = node.WithModifiers(resultModifiers);
        }

        return base.VisitMethodDeclaration(node);
    }

    private bool ShouldIgnore(SyntaxNode node)
    {
        try
        {
            ISymbol? symbol = _semanticModel.GetDeclaredSymbol(node);

            if (symbol is not INamedTypeSymbol typeSymbol)
                return false;

            return _excludedFromOpenNamespaces.Contains(typeSymbol.ContainingNamespace.Name);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryOpenModifiers(
        SyntaxTokenList modifiers,
        out SyntaxTokenList resultModifiers,
        bool replaceProtected = false,
        bool replaceEmpty = false)
    {
        resultModifiers = modifiers;
        var changed = false;

        if (replaceProtected is false && modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ProtectedKeyword)))
        {
            return false;
        }

        for (var i = 0; i < resultModifiers.Count;)
        {
            SyntaxToken modifier = resultModifiers[i];

            if (modifier.Kind() is
                SyntaxKind.PrivateKeyword
                or SyntaxKind.ProtectedKeyword
                or SyntaxKind.InternalKeyword
                or SyntaxKind.PublicKeyword)
            {
                changed = true;
                resultModifiers = resultModifiers.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }

        if (changed)
        {
            resultModifiers = resultModifiers.Insert(
                0,
                Token(modifiers[0].LeadingTrivia, SyntaxKind.PublicKeyword, modifiers[^1].TrailingTrivia));
        }
        else if (replaceEmpty && modifiers.Any() is false)
        {
            resultModifiers = [Token(SyntaxKind.PublicKeyword)];
            return true;
        }

        return changed;
    }
}
