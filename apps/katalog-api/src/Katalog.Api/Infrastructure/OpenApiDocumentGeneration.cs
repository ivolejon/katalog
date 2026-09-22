using System.Reflection;

namespace Katalog.Api.Infrastructure;

/// <summary>
/// Detects the build-time OpenAPI document generation host (<c>GetDocument.Insider</c>) that
/// <c>Microsoft.Extensions.ApiDescription.Server</c> launches during <c>dotnet build</c>
/// (arch report §2.8, fallgropar §6.3). All setups that touch external systems or eagerly
/// validate options must be guarded with <see cref="IsActive"/> so the project builds without
/// a live Postgres or Spotify.
/// </summary>
public static class OpenApiDocumentGeneration
{
    private const string GeneratorAssemblyName = "GetDocument.Insider";

    /// <summary>
    /// True when the process was started by the build-time OpenAPI document generator.
    /// </summary>
    public static bool IsActive { get; } =
        Assembly.GetEntryAssembly()?.GetName().Name == GeneratorAssemblyName;
}
