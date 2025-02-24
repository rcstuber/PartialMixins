﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PartialMixins
{
    [Generator]
    public class PartialMixin : ISourceGenerator
    {
        private const string attributeText = @"
namespace Mixin
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
    public sealed class MixinAttribute : System.Attribute
    {
        public MixinAttribute(System.Type toImplement)
        {
        }
    }
}
";



        public void Initialize(GeneratorInitializationContext context)
        {
            // Register the attribute source
            context.RegisterForPostInitialization((i) => i.AddSource("MixinAttribute.cs", attributeText));

            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                ExecuteInternal(context);

            }
            catch (Exception e)
            {

                string lines = string.Empty;
                StringReader reader = new StringReader(e.ToString());

                string line;
                do
                {
                    line = reader.ReadLine();
                    lines += "\n#error " + line ?? string.Empty;
                }
                while (line != null);

                SourceText txt = SourceText.From(lines, System.Text.Encoding.UTF8);

                context.AddSource($"Error_mixins.cs", txt);
            }
        }

        private static void ExecuteInternal(GeneratorExecutionContext context)
        {
            // retrieve the populated receiver 
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
            {
                return;
            }

            // get the added attribute, and INotifyPropertyChanged
            INamedTypeSymbol mixinAttribute = context.Compilation.GetTypeByMetadataName("Mixin.MixinAttribute");
            INamedTypeSymbol parameterAttribute = context.Compilation.GetTypeByMetadataName("Mixin.SubstituteAttribute");


            // We do use the correct compareer...
#pragma warning disable RS1024 // Compare symbols correctly
            IEnumerable<INamedTypeSymbol> typesToExtend = new HashSet<INamedTypeSymbol>(receiver.Types.Where(t => t.GetAttributes().Any(x => x.AttributeClass.Equals(mixinAttribute, SymbolEqualityComparer.Default))), SymbolEqualityComparer.Default);
#pragma warning restore RS1024 // Compare symbols correctly

            typesToExtend = typesToExtend.OrderTopological(elementThatDependsOnOther =>
            {
                IEnumerable<AttributeData> toImplement = elementThatDependsOnOther.GetAttributes().Where(x => x.AttributeClass.Equals(mixinAttribute, SymbolEqualityComparer.Default));
                INamedTypeSymbol[] implementationSymbol = toImplement
                .Select(currentMixinAttribute => (currentMixinAttribute.ConstructorArguments.First().Value as INamedTypeSymbol).ConstructedFrom)
                .Where(x => typesToExtend.Contains(x, SymbolEqualityComparer.Default)).ToArray();
                return implementationSymbol;
            });




            CSharpCompilation compilation = (CSharpCompilation)context.Compilation;

            foreach (INamedTypeSymbol originalType in typesToExtend)
            {
                IEnumerable<AttributeData> toImplement = originalType.GetAttributes().Where(x => x.AttributeClass.Equals(mixinAttribute, SymbolEqualityComparer.Default));
                List<TypeDeclarationSyntax> typeExtensions = new List<TypeDeclarationSyntax>();
                
                foreach (AttributeData currentMixinAttribute in toImplement)
                {
                    INamedTypeSymbol implementationSymbol = (currentMixinAttribute.ConstructorArguments.First().Value as INamedTypeSymbol);
                    INamedTypeSymbol updatetedImplementationSymbol = compilation.GetTypeByMetadataName(GetFullQualifiedName(implementationSymbol));
                    // Get Generic Typeparameter
                    System.Diagnostics.Debug.Assert(updatetedImplementationSymbol != null, $"updatetedImplementationSymbol is null {implementationSymbol} / {GetFullQualifiedName(implementationSymbol)}");

                    Dictionary<ITypeParameterSymbol, ITypeSymbol> typeParameterMapping = implementationSymbol.TypeParameters
                                            .Zip(implementationSymbol.TypeArguments, (parameter, argumet) => new { parameter, argumet })
                                        .ToDictionary(x => x.parameter, x => x.argumet, new TypeParameterComparer());

                    implementationSymbol = updatetedImplementationSymbol; // Waited until we saved the TypeParameters.

                    foreach (TypeDeclarationSyntax originalImplementaionSyntaxNode in implementationSymbol.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).Cast<TypeDeclarationSyntax>())
                    {
                        SemanticModel semanticModel = compilation.GetSemanticModel(originalImplementaionSyntaxNode.SyntaxTree);

                        TypeDeclarationSyntax changedImplementaionSyntaxNode = originalImplementaionSyntaxNode;

                        TypeParameterImplementer typeParameterImplementer = new TypeParameterImplementer(semanticModel, typeParameterMapping, originalType, implementationSymbol);
                        changedImplementaionSyntaxNode = (TypeDeclarationSyntax)typeParameterImplementer.Visit(changedImplementaionSyntaxNode);

                        MethodAttributor AttributeGenerator = new MethodAttributor(changedImplementaionSyntaxNode, originalType);
                        changedImplementaionSyntaxNode = (TypeDeclarationSyntax)AttributeGenerator.Visit(changedImplementaionSyntaxNode);

                        TypeDeclarationSyntax newClass = (originalType.IsReferenceType ?
                            SyntaxFactory.ClassDeclaration(originalType.Name) : (TypeDeclarationSyntax)SyntaxFactory.StructDeclaration(originalType.Name))
                            .WithBaseList(changedImplementaionSyntaxNode.BaseList)
                            .WithMembers(changedImplementaionSyntaxNode.Members);
                        if (originalType?.TypeParameters.Any() ?? false)
                        {
                            newClass = newClass.WithTypeParameterList(GetTypeParameters(originalType));
                        }

                        switch (originalType.DeclaredAccessibility)
                        {
                            case Accessibility.NotApplicable:
                                break;
                            case Accessibility.Private:
                                newClass = newClass.AddModifiers(SyntaxFactory.ParseToken("private"));
                                break;
                            case Accessibility.ProtectedAndInternal:
                                newClass = newClass.AddModifiers(SyntaxFactory.ParseToken("protected"));
                                newClass = newClass.AddModifiers(SyntaxFactory.ParseToken("internal"));
                                break;
                            case Accessibility.Protected:
                                newClass = newClass.AddModifiers(SyntaxFactory.ParseToken("protected"));
                                break;
                            case Accessibility.Internal:
                                newClass = newClass.AddModifiers(SyntaxFactory.ParseToken("internal"));
                                break;
                            case Accessibility.ProtectedOrInternal:
                                newClass = newClass.AddModifiers(SyntaxFactory.ParseToken("protected"));
                                newClass = newClass.AddModifiers(SyntaxFactory.ParseToken("internal"));
                                break;
                            case Accessibility.Public:
                                newClass = newClass.AddModifiers(SyntaxFactory.ParseToken("public"));
                                break;
                            default:
                                break;
                        }

                        if (originalType.IsStatic)
                        {
                            newClass = newClass.AddModifiers(SyntaxFactory.ParseToken("static"));
                        }

                        if (!newClass.Modifiers.Any(x => x.Text == "partial"))
                        {
                            newClass = newClass.AddModifiers(SyntaxFactory.ParseToken("partial"));
                        }

                        typeExtensions.Add(newClass);
                        //if (compilation is CSharpCompilation csCompilation)
                        //{
                        //}

                        //newClasses.Add(newNamespaceDeclaration);
                    }
                }


                NamespaceDeclarationSyntax newNamespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(GetNsName(originalType.ContainingNamespace)))
                    .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(typeExtensions));
                CompilationUnitSyntax compilationUnit = SyntaxFactory.CompilationUnit().WithMembers(SyntaxFactory.List(new MemberDeclarationSyntax[] { newNamespaceDeclaration }));

                SyntaxTree syntaxTree = compilationUnit.SyntaxTree;
                syntaxTree = syntaxTree.WithRootAndOptions(syntaxTree.GetRoot(), new CSharpParseOptions(languageVersion: compilation.LanguageVersion) { });
                syntaxTree = syntaxTree.GetRoot().NormalizeWhitespace().SyntaxTree;

                compilation = compilation.AddSyntaxTrees(syntaxTree);

                SyntaxNode formated = syntaxTree.GetRoot();
                SourceText txt = formated.GetText(System.Text.Encoding.UTF8);

                //string lines = string.Empty;
                //var reader = new StringReader(txt.ToString());

                //string line;
                //do
                //{
                //    line = reader.ReadLine();
                //    lines += "\n#error " + line ?? string.Empty;
                //}
                //while (line != null);
                //txt = SourceText.From(lines, System.Text.Encoding.UTF8);
                File.WriteAllText($"/Users/rstuber/Documents/Projekte/WorldCup/libs/PartialMixins/Tests/{originalType.Name}_mixins.debug", txt.ToString());

                context.AddSource($"{originalType.Name}_mixins.cs", txt);
            }
        }

        private static TypeParameterListSyntax GetTypeParameters(INamedTypeSymbol originalType)
        {
            IEnumerable<TypeParameterSyntax> typeParametrs = originalType.TypeParameters.Select(x => SyntaxFactory.TypeParameter(x.Name));
            return SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(typeParametrs));
        }


        internal static string GetNsName(INamespaceSymbol ns)
        {
            if (ns == null)
            {
                return null;
            }

            if (ns.ContainingNamespace != null && !string.IsNullOrWhiteSpace(ns.ContainingNamespace.Name))
            {
                return $"{GetNsName(ns.ContainingNamespace)}.{ns.Name}";
            }

            return ns.Name;
        }

        internal static string GetFullQualifiedName(ISymbol typeSymbol, bool getMetadata = false)
        {
            if (typeSymbol is IArrayTypeSymbol)
            {
                IArrayTypeSymbol arraySymbol = typeSymbol as IArrayTypeSymbol;
                return $"{GetFullQualifiedName(arraySymbol.ElementType)}[]";
            }
            string ns = GetNsName(typeSymbol.ContainingNamespace);
            if (!string.IsNullOrWhiteSpace(ns))
            {

                string name = GetName(typeSymbol, getMetadata);
                if (name.StartsWith(ns))
                {
                    return name;
                }
                else
                {
                    return $"{ns}.{name}";

                }
            }

            return typeSymbol.MetadataName;
        }

        private static string GetName(ISymbol typeSymbol, bool getmetadata)
        {
            if (getmetadata)
            {
                return typeSymbol.MetadataName;
            }

            return typeSymbol.ToString();
        }



        /// <summary>
        /// Created on demand before each generation pass
        /// </summary>
        private class SyntaxReceiver : ISyntaxContextReceiver
        {
            public List<INamedTypeSymbol> Types { get; } = new();

            /// <summary>
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
            /// </summary>
            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                // any field with at least one attribute is a candidate for property generation
                if (context.Node is TypeDeclarationSyntax typeDeclaration
                    && typeDeclaration.AttributeLists.Count > 0)
                {
                    // Get the symbol being declared by the field, and keep it if its annotated
                    INamedTypeSymbol typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
                    if (typeSymbol.GetAttributes().Any(ad => ad.AttributeClass.ToDisplayString() == "Mixin.MixinAttribute"))
                    {
                        Types.Add(typeSymbol);
                    }
                }
            }
        }
    }

}

