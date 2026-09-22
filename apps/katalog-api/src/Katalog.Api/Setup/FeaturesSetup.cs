using FluentValidation;
using Katalog.Api.Features.Artists;
using Katalog.Api.Features.Labels;
using Katalog.Api.Features.Releases;
using Katalog.Api.Features.Releases.Polling;
using Katalog.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Katalog.Api.Setup;

public static class FeaturesSetup
{
    public static IServiceCollection AddFeatures(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddValidatorsFromAssembly(typeof(FeaturesSetup).Assembly);

        services.AddScoped<CreateLabel>();
        services.AddScoped<GetLabels>();
        services.AddScoped<UpdateLabel>();
        services.AddScoped<DeleteLabel>();
        services.AddScoped<AddArtistToLabel>();
        services.AddScoped<RemoveArtistFromLabel>();
        services.AddScoped<SearchArtists>();
        services.AddScoped<GetLabelReleases>();

        services.AddScoped<ReleasePoller>();

        // The polling worker must not run during build-time OpenAPI generation (no live database).
        if (!OpenApiDocumentGeneration.IsActive)
        {
            services.AddHostedService<ReleasesPollingService>();
        }

        return services;
    }
}
