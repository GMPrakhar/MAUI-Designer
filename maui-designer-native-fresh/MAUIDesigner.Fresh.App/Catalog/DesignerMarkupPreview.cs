namespace MAUIDesigner.Fresh.App.Catalog;

public static class DesignerMarkupPreview
{
    public static bool TryGetLiteral(string markup, out string literal)
    {
        literal = string.Empty;
        if (!markup.StartsWith("{Binding", StringComparison.Ordinal) ||
            !markup.EndsWith('}'))
        {
            return false;
        }

        foreach (string argument in SplitArguments(markup[1..^1]))
        {
            int separator = argument.IndexOf('=');
            if (separator < 0 ||
                !argument[..separator].Trim().Equals(
                    "FallbackValue",
                    StringComparison.Ordinal))
            {
                continue;
            }

            literal = Unquote(argument[(separator + 1)..].Trim());
            return true;
        }

        return false;
    }

    private static IEnumerable<string> SplitArguments(string body)
    {
        int start = 0;
        int nestedDepth = 0;
        char quote = '\0';
        for (int index = 0; index < body.Length; index++)
        {
            char current = body[index];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
            }
            else if (current == '{')
            {
                nestedDepth++;
            }
            else if (current == '}')
            {
                nestedDepth = Math.Max(0, nestedDepth - 1);
            }
            else if (current == ',' && nestedDepth == 0)
            {
                yield return body[start..index].Trim();
                start = index + 1;
            }
        }

        yield return body[start..].Trim();
    }

    private static string Unquote(string value) =>
        value.Length >= 2 &&
        ((value[0] == '\'' && value[^1] == '\'') ||
         (value[0] == '"' && value[^1] == '"'))
            ? value[1..^1]
            : value;
}
