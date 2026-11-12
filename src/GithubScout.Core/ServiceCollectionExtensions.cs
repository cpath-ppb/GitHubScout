using GithubScout.Core.Abstractions;
using GithubScout.Core.Configuration;
using GithubScout.Core.Services;
using GithubScout.Core.Services.KeepAwake;
using GithubScout.Core.Services.Notifiers;
using GithubScout.Core.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;

namespace GithubScout.Core;

/// <summary>Composition root for wiring up every GithubScout.Core service.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGithubScoutCore(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        services.Configure<GithubScoutOptions>(configuration.GetSection(GithubScoutOptions.SectionName));

        services.AddSingleton<ITokenProvider, ConfigurationTokenProvider>();

        services.AddSingleton<IGitHubClient>(sp =>
        {
            var tokenProvider = sp.GetRequiredService<ITokenProvider>();
            var client = new GitHubClient(new ProductHeaderValue("GithubScout"));
            var token = tokenProvider.GetToken();
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.Credentials = new Credentials(token);
            }

            return client;
        });

        services.AddTransient<GitHubAuthHeaderHandler>();
        services.AddHttpClient<GitHubGraphQLReviewClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GithubScout");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }).AddHttpMessageHandler<GitHubAuthHeaderHandler>();

        services.AddSingleton<IGitHubWatcherClient, OctokitGitHubWatcherClient>();

        services.AddSingleton<IWatchStateRepository>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GithubScoutOptions>>().Value;
            return new InMemoryWatchStateRepository(options.MaxActivityEntries);
        });

        services.AddSingleton<IWatchlistStore>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GithubScoutOptions>>().Value;
            var path = Path.IsPathRooted(options.WatchlistFilePath)
                ? options.WatchlistFilePath
                : Path.Combine(contentRootPath, options.WatchlistFilePath);
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JsonFileWatchlistStore>>();
            return new JsonFileWatchlistStore(path, logger);
        });

        services.AddSingleton<INotifier, ActivityLogNotifier>();
        services.AddSingleton<INotifier, WindowsToastNotifier>();

        services.AddSingleton(new KeepAwakeDiagnostics(KeepAwakeDiagnostics.DefaultPath));
        services.AddSingleton<KeepAwakeServiceFactory>();
        services.AddSingleton<IKeepAwakeService>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GithubScoutOptions>>().Value;
            return sp.GetRequiredService<KeepAwakeServiceFactory>().Create(options.EnableKeepAwake);
        });

        services.AddHostedService<KeepAwakeHostedService>();
        services.AddHostedService<WatchlistPollingService>();

        return services;
    }
}
