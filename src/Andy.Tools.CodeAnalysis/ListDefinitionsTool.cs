using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library;
using Andy.Tools.Library.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Andy.Tools.CodeAnalysis;

/// <summary>
/// Tool that returns the C# symbol outline (definitions) of a source file using Roslyn.
/// <para>
/// Parsing is syntax-only (<see cref="CSharpSyntaxTree.ParseText(string, CSharpParseOptions, string, Encoding, CancellationToken)"/>):
/// no compilation or metadata references are needed to produce an outline, which keeps it fast and
/// lets it work on files in isolation. Roslyn is error-tolerant, so files with syntax errors still
/// yield whatever definitions could be parsed.
/// </para>
/// </summary>
public class ListDefinitionsTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "list_definitions",
        Name = "List Definitions",
        Description = "Returns the C# symbol outline of a source file (namespaces, types, methods, "
            + "constructors, properties, fields, events) using Roslyn syntax-only parsing.",
        Version = "1.0.0",
        Category = ToolCategory.Development,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Parameters =
        [
            new()
            {
                Name = "file_path",
                Description = "The path to the C# (.cs) file to outline",
                Type = "string",
                Required = true
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var filePath = GetParameter<string>(parameters, "file_path");

        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ToolResults.InvalidParameter("file_path", filePath, "A file path is required");
            }

            // Resolve and confine the path.
            var safePath = ToolHelpers.GetSafePath(filePath, context.WorkingDirectory);

            if (!ToolHelpers.IsPathWithinAllowedPaths(safePath, context.Permissions))
            {
                return ToolResults.Failure($"Path '{safePath}' is not within allowed paths", "PATH_NOT_ALLOWED");
            }

            if (!File.Exists(safePath))
            {
                return ToolResults.FileNotFound(safePath);
            }

            if (!string.Equals(Path.GetExtension(safePath), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                return ToolResults.Failure(
                    $"File '{safePath}' is not a C# (.cs) file",
                    "UNSUPPORTED_FILE_TYPE");
            }

            var source = await ToolHelpers.ReadTextFileAsync(safePath, cancellationToken: context.CancellationToken);

            var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: context.CancellationToken);
            var root = await tree.GetRootAsync(context.CancellationToken);

            var collector = new DefinitionCollector();
            collector.Visit(root);

            var definitions = collector.Definitions
                .OrderBy(d => d.StartLine)
                .ThenBy(d => d.EndLine)
                .Select(d => (object?)new Dictionary<string, object?>
                {
                    ["name"] = d.Name,
                    ["kind"] = d.Kind,
                    ["containerName"] = d.ContainerName,
                    ["startLine"] = d.StartLine,
                    ["endLine"] = d.EndLine,
                    ["signature"] = d.Signature
                })
                .ToList();

            var metadata = new Dictionary<string, object?>
            {
                ["file_path"] = safePath,
                ["definition_count"] = definitions.Count
            };

            return ToolResults.Success(
                definitions,
                $"Found {definitions.Count} definition{(definitions.Count == 1 ? string.Empty : "s")}",
                metadata);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            // GetSafePath throws ArgumentException for paths that escape the working directory.
            return ToolResults.Failure(ex.Message, "PATH_NOT_ALLOWED", innerException: ex);
        }
        catch (Exception ex)
        {
            return ToolResults.Failure(
                $"Failed to list definitions for '{filePath}': {ex.Message}",
                "ANALYSIS_FAILED",
                innerException: ex);
        }
    }

    private sealed record Definition(
        string Name,
        string Kind,
        string? ContainerName,
        int StartLine,
        int EndLine,
        string Signature);

    /// <summary>
    /// Walks a C# syntax tree and records each definition's name, kind, enclosing container,
    /// 1-based line span and a short modifier/signature string.
    /// </summary>
    private sealed class DefinitionCollector : CSharpSyntaxWalker
    {
        private readonly Stack<string> _containers = new();

        public List<Definition> Definitions { get; } = [];

        private string? CurrentContainer => _containers.Count > 0 ? _containers.Peek() : null;

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var name = node.Name.ToString();
            Add(name, "namespace", node, "namespace");
            _containers.Push(name);
            base.VisitNamespaceDeclaration(node);
            _containers.Pop();
        }

        public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
        {
            var name = node.Name.ToString();
            Add(name, "namespace", node, "namespace");
            _containers.Push(name);
            base.VisitFileScopedNamespaceDeclaration(node);
            _containers.Pop();
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            => VisitType(node, node.Identifier.Text, "class", node.Modifiers, () => base.VisitClassDeclaration(node));

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
            => VisitType(node, node.Identifier.Text, "struct", node.Modifiers, () => base.VisitStructDeclaration(node));

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
            => VisitType(node, node.Identifier.Text, "interface", node.Modifiers, () => base.VisitInterfaceDeclaration(node));

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            var kind = node.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record";
            VisitType(node, node.Identifier.Text, kind, node.Modifiers, () => base.VisitRecordDeclaration(node));
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
            => VisitType(node, node.Identifier.Text, "enum", node.Modifiers, () => base.VisitEnumDeclaration(node));

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var signature = $"{Modifiers(node.Modifiers)}{node.ReturnType} {node.Identifier.Text}{node.ParameterList}".Trim();
            Add(node.Identifier.Text, "method", node, signature);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var signature = $"{Modifiers(node.Modifiers)}{node.Identifier.Text}{node.ParameterList}".Trim();
            Add(node.Identifier.Text, "constructor", node, signature);
            base.VisitConstructorDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var signature = $"{Modifiers(node.Modifiers)}{node.Type} {node.Identifier.Text}".Trim();
            Add(node.Identifier.Text, "property", node, signature);
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            var signature = $"{Modifiers(node.Modifiers)}event {node.Type} {node.Identifier.Text}".Trim();
            Add(node.Identifier.Text, "event", node, signature);
            base.VisitEventDeclaration(node);
        }

        public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            foreach (var variable in node.Declaration.Variables)
            {
                var signature = $"{Modifiers(node.Modifiers)}event {node.Declaration.Type} {variable.Identifier.Text}".Trim();
                Add(variable.Identifier.Text, "event", variable, signature);
            }

            base.VisitEventFieldDeclaration(node);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            foreach (var variable in node.Declaration.Variables)
            {
                var signature = $"{Modifiers(node.Modifiers)}{node.Declaration.Type} {variable.Identifier.Text}".Trim();
                Add(variable.Identifier.Text, "field", variable, signature);
            }

            base.VisitFieldDeclaration(node);
        }

        private void VisitType(SyntaxNode node, string name, string kind, SyntaxTokenList modifiers, Action visitChildren)
        {
            Add(name, kind, node, $"{Modifiers(modifiers)}{kind} {name}".Trim());
            _containers.Push(name);
            visitChildren();
            _containers.Pop();
        }

        private void Add(string name, string kind, SyntaxNode node, string signature)
        {
            var span = node.GetLocation().GetLineSpan();
            Definitions.Add(new Definition(
                name,
                kind,
                CurrentContainer,
                span.StartLinePosition.Line + 1,
                span.EndLinePosition.Line + 1,
                signature));
        }

        private static string Modifiers(SyntaxTokenList modifiers)
        {
            if (modifiers.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(" ", modifiers.Select(m => m.Text)) + " ";
        }
    }
}
