using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace AudioResearch.Cli;

/// <summary>Shared JSON output settings for the CLI.</summary>
internal static class Json
{
    // A non-default options instance needs an explicit resolver in .NET 8 before
    // it can serialize a JsonNode tree.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static string Write(JsonNode node) => node.ToJsonString(Options);
}
