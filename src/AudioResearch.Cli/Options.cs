using System.Globalization;

namespace AudioResearch.Cli;

/// <summary>
/// Tiny option parser for "--key value" / "--flag" arguments plus leading
/// positional arguments. Hand-rolled to keep the CLI dependency-free and
/// fully deterministic.
/// </summary>
public sealed class Options
{
    private readonly Dictionary<string, string> _named = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Positionals { get; }

    private Options(List<string> positionals)
    {
        Positionals = positionals;
    }

    public static Options Parse(IReadOnlyList<string> args)
    {
        var positionals = new List<string>();
        var options = new Options(positionals);

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                string key = arg[2..];
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options._named[key] = args[i + 1];
                    i++;
                }
                else
                {
                    options._flags.Add(key);
                }
            }
            else
            {
                positionals.Add(arg);
            }
        }

        return options;
    }

    public bool HasFlag(string key) => _flags.Contains(key);

    public string? GetString(string key) => _named.TryGetValue(key, out string? v) ? v : null;

    public double GetDouble(string key, double fallback)
    {
        if (_named.TryGetValue(key, out string? v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            return d;
        }

        return fallback;
    }

    public int GetInt(string key, int fallback)
    {
        if (_named.TryGetValue(key, out string? v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
        {
            return n;
        }

        return fallback;
    }
}
