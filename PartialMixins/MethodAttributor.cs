using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartialMixins
{
    class MethodAttributor : CSharpSyntaxRewriter
    {

        private const string GENERATOR_ATTRIBUTE_NAME = "global::System.CodeDom.Compiler.GeneratedCodeAttribute";
        private readonly AttributeListSyntax[] generatedAttribute;
        private readonly CSharpSyntaxNode currentDeclaration;

        private INamedTypeSymbol originalType;

        public MethodAttributor(CSharpSyntaxNode currentDeclaration, INamedTypeSymbol originalType)
        {
            this.originalType = originalType;

            this.generatedAttribute = new AttributeListSyntax[] { SyntaxFactory.AttributeList(
                            SyntaxFactory.SeparatedList(new AttributeSyntax[] {
                            SyntaxFactory.Attribute(SyntaxFactory.ParseName( GENERATOR_ATTRIBUTE_NAME),
                                SyntaxFactory.AttributeArgumentList(
                                    SyntaxFactory.SeparatedList(new AttributeArgumentSyntax[] {
                                        SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression( SyntaxKind.StringLiteralExpression, SyntaxFactory.ParseToken("\"Mixin Task\""))),
                                        SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression( SyntaxKind.StringLiteralExpression, SyntaxFactory.ParseToken($"\"{this.GetType().Assembly.GetName().Version}\"")))
                                    }))
                            )
                        }))};
            this.currentDeclaration = currentDeclaration;
        }

        private IEnumerable<CSharpSyntaxNode> GetSyntaxNodesForType(INamedTypeSymbol type)
        {
            return type.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).Cast<TypeDeclarationSyntax>();
        }

        private bool NodeExists(SyntaxNode node)
        {
            var type = originalType;
            while(type != null) {
                foreach(var impl in GetSyntaxNodesForType(type)) {
                    if(null == impl)
                        continue;
                    foreach(var declared in impl.ChildNodes()) {
                        /*if(node is MethodDeclarationSyntax method)
                            node = method.WithModifiers(method.Modifiers.Remove(
                                method.Modifiers.First(x => x.Kind() == SyntaxKind.OverrideKeyword || x.Kind() == SyntaxKind.VirtualKeyword))
                            );*/
                        if(declared.IsEquivalentTo(node, true))
                            return true;
                    }
                }
                type = type.BaseType;
            }
            return false;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if(NodeExists(node)) {
                var newIdent = SyntaxFactory.Identifier(node.Identifier + "_Mixin");
                var baseMethod = node.WithIdentifier(newIdent);
                return base.VisitMethodDeclaration(baseMethod);
            }
            if (node.Modifiers.Any(x => x.Kind() == SyntaxKind.AbstractKeyword))
                node = node.WithModifiers(node.Modifiers.Remove(node.Modifiers.First(x => x.Kind() == SyntaxKind.AbstractKeyword)).Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword)));

            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitMethodDeclaration(node);
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {

            if (node != this.currentDeclaration && !node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitClassDeclaration(node);
        }

        public override SyntaxNode VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitDelegateDeclaration(node);
        }

        public override SyntaxNode VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitDestructorDeclaration(node);
        }

        public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitEnumDeclaration(node);
        }
        public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            if(NodeExists(node))
                return null;
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitFieldDeclaration(node);
        }

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitInterfaceDeclaration(node);
        }

        public override SyntaxNode VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            if (node != this.currentDeclaration && !node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitRecordDeclaration(node);
        }

        public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        {
            if (node != this.currentDeclaration && !node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitStructDeclaration(node);
        }


        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitConstructorDeclaration(node);
        }

        public override SyntaxNode VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitConversionOperatorDeclaration(node);
        }

        public override SyntaxNode VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitOperatorDeclaration(node);
        }

        public override SyntaxNode VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitEventFieldDeclaration(node);
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if(NodeExists(node))
                return null;
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitPropertyDeclaration(node);
        }

        public override SyntaxNode VisitEventDeclaration(EventDeclarationSyntax node)
        {
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitEventDeclaration(node);
        }

        public override SyntaxNode VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            if (!node.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToFullString() == GENERATOR_ATTRIBUTE_NAME)))
                return node.AddAttributeLists(this.generatedAttribute);
            return base.VisitIndexerDeclaration(node);
        }
    }

}
