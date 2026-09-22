using Katalog.Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Setup;

public static class OptionsSetup
{
    public static IServiceCollection AddKatalogOptions(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        // ValidateOnStart fails the app early when credentials are missing - but it is skipped
        // during build-time OpenAPI generation, where placeholder values are supplied
        // (arch report §6.3/§6.9).
        if (!OpenApiDocumentGeneration.IsActive)
        {
            services.AddOptions<SpotifyOptions>()
                .Bind(configuration.GetSection(SpotifyOptions.SectionName))
                .Validate(sp =>
                        !string.IsNullOrWhiteSpace(sp.ClientId)
                        && !string.IsNullOrWhiteSpace(sp.ClientSecret)
                        && Uri.IsWellFormedUriString(sp.BaseUrl, UriKind.Absolute)
                        && Uri.IsWellFormedUriString(sp.AccountsBaseUrl, UriKind.Absolute),
                    "Spotify ClientId/ClientSecret must be configured and BaseUrls must be well-formed URIs.")
                .ValidateOnStart();

            services.AddOptions<PollingOptions>()
                .Bind(configuration.GetSection(PollingOptions.SectionName))
                .Validate(PollingOptions.Validate)
                .ValidateOnStart();
        }

        return services;
    }
}
