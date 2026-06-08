using System.Text.RegularExpressions;

namespace NotificationService.Application;

public sealed partial class TemplateRenderer
{
    public string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        return PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;

            if (!values.TryGetValue(key, out var value))
            {
                throw new InvalidOperationException($"Template value '{key}' was not provided");
            }

            return value;
        });
    }

    [GeneratedRegex(@"\{\{([a-zA-Z0-9_.-]+)\}\}")]
    private static partial Regex PlaceholderRegex();
}
