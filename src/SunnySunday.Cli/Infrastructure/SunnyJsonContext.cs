using System.Text.Json.Serialization;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Source-generated JSON serializer context for all contract types used by the CLI.
/// Required for trimmed/NativeAOT publish.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SyncRequest))]
[JsonSerializable(typeof(SyncResponse))]
[JsonSerializable(typeof(SettingsResponse))]
[JsonSerializable(typeof(UpdateSettingsRequest))]
[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(ExclusionsResponse))]
[JsonSerializable(typeof(SetWeightRequest))]
[JsonSerializable(typeof(List<WeightedHighlightDto>))]
internal partial class SunnyJsonContext : JsonSerializerContext;
