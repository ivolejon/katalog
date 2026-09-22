using Katalog.Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Katalog.Api.Setup;

public static class OpenApiGenerationSetup
{
    /// <summary>
    /// The build-time generator starts the app like normal, so placeholder settings must be
    /// supplied (arch report §2.8: appsettings.OpenApiGeneration.json with placeholder
    /// connections, loaded only by the GetDocument.Insider process).
    /// </summary>
    private const string SettingsFileName = "appsettings.OpenApiGeneration.json";

    public static bool AddOpenApiDocumentGenerationSettings(this IHostApplicationBuilder builder)
    {
        if (!OpenApiDocumentGeneration.IsActive)
        {
            return false;
        }

        builder.Configuration.AddJsonFile(SettingsFileName, optional: false, reloadOnChange: false);
        return true;
    }
}
