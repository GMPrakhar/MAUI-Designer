using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace MAUIDesigner.Fresh.Core.Documents;

public static class DesignerNodeCloner
{
    public static DesignerNode CloneSubtree(
        DesignerNode source,
        Func<ElementId>? createId = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        createId ??= ElementId.CreateUnique;

        var generatedIds = new HashSet<ElementId>();
        var ids = new Dictionary<ElementId, ElementId>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        BuildClonePlan(source, createId, generatedIds, ids, names);
        return Clone(source, ids, names);
    }

    private static void BuildClonePlan(
        DesignerNode source,
        Func<ElementId> createId,
        HashSet<ElementId> generatedIds,
        Dictionary<ElementId, ElementId> ids,
        Dictionary<string, string> names)
    {
        ElementId id = createId();
        if (!generatedIds.Add(id))
        {
            throw new InvalidOperationException($"The clone id factory produced duplicate id '{id}'.");
        }

        ids.Add(source.Id, id);
        if (source.Properties.TryGetValue("x:Name", out DesignerValue? name) &&
            !string.IsNullOrWhiteSpace(name.Text))
        {
            names.TryAdd(name.Text, CreateCloneName(name.Text, id));
        }

        foreach (DesignerNode child in source.Children)
        {
            BuildClonePlan(child, createId, generatedIds, ids, names);
        }
    }

    private static DesignerNode Clone(
        DesignerNode source,
        IReadOnlyDictionary<ElementId, ElementId> ids,
        IReadOnlyDictionary<string, string> names)
    {
        ImmutableArray<DesignerNode>.Builder children =
            ImmutableArray.CreateBuilder<DesignerNode>(source.Children.Length);
        foreach (DesignerNode child in source.Children)
        {
            children.Add(Clone(child, ids, names));
        }

        ImmutableDictionary<string, DesignerValue> properties = names.Count == 0
            ? source.Properties
            : RewriteProperties(source.Properties, names);
        ImmutableArray<XamlSyntaxFragment> preservedContent = names.Count == 0
            ? source.PreservedContent
            : source.PreservedContent
                .Select(fragment => new XamlSyntaxFragment(
                    RewritePreservedAttributes(fragment.Xml, names)))
                .ToImmutableArray();
        return source with
        {
            Id = ids[source.Id],
            Properties = properties,
            Children = children.MoveToImmutable(),
            PreservedContent = preservedContent
        };
    }

    private static ImmutableDictionary<string, DesignerValue> RewriteProperties(
        ImmutableDictionary<string, DesignerValue> properties,
        IReadOnlyDictionary<string, string> names)
    {
        var rewritten = properties.ToBuilder();
        if (properties.TryGetValue("x:Name", out DesignerValue? name) &&
            names.TryGetValue(name.Text, out string? cloneName))
        {
            rewritten["x:Name"] = name with { Text = cloneName };
        }

        foreach ((string propertyName, DesignerValue value) in properties)
        {
            if (propertyName == "x:Name" || value.Kind == DesignerValueKind.Literal)
            {
                continue;
            }

            rewritten[propertyName] = value with
            {
                Text = RewriteReferences(value.Text, names)
            };
        }

        return rewritten.ToImmutable();
    }

    private static string RewriteReferences(
        string value,
        IReadOnlyDictionary<string, string> names)
    {
        string rewritten = value;
        foreach ((string original, string replacement) in names)
        {
            rewritten = Regex.Replace(
                rewritten,
                $@"(?<prefix>\{{\s*x:Reference\s+(?:Name\s*=\s*)?)(?:(?<quote>[""']|&quot;|&apos;){Regex.Escape(original)}\k<quote>|{Regex.Escape(original)})(?=\s*[,}}])",
                match => match.Groups["prefix"].Value +
                    match.Groups["quote"].Value +
                    replacement +
                    match.Groups["quote"].Value,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(
                rewritten,
                $@"(?<prefix>\bElementName\s*=\s*)(?:(?<quote>[""']|&quot;|&apos;){Regex.Escape(original)}\k<quote>|{Regex.Escape(original)})(?=\s*[,}}])",
                match => match.Groups["prefix"].Value +
                    match.Groups["quote"].Value +
                    replacement +
                    match.Groups["quote"].Value,
                RegexOptions.CultureInvariant);
        }

        return rewritten;
    }

    private static string RewritePreservedAttributes(
        string xml,
        IReadOnlyDictionary<string, string> names) =>
        Regex.Replace(
            xml,
            @"(?<prefix>\s[^\s=<>/]+\s*=\s*)(?<quote>[""'])(?<value>.*?)\k<quote>",
            match => match.Groups["prefix"].Value +
                match.Groups["quote"].Value +
                RewriteReferences(match.Groups["value"].Value, names) +
                match.Groups["quote"].Value,
            RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static string CreateCloneName(string original, ElementId id)
    {
        string suffix = new(
            id.Value.Select(character =>
                    char.IsLetterOrDigit(character) ? character : '_')
                .ToArray());
        return $"{original}_Copy_{suffix}";
    }
}
