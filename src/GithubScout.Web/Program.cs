using GithubScout.Core;
using GithubScout.Web.ConsoleDashboard;
using GithubScout.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGithubScoutCore(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddHostedService<ConsoleDashboardService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.MapWatchlistEndpoints();

app.Run();

/// <summary>Marker partial class exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
