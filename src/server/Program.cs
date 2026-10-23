using MegabonkTogether.Common.Models;
using MegabonkTogether.Server;
using System.Globalization;
using MegabonkTogether.Server.Services;
using Microsoft.AspNetCore.HttpOverrides;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<ConfigOptions>(builder.Configuration.GetSection("Config"));
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddSingleton<WebSocketHandler>();
builder.Services.AddSingleton<IRendezVousServer, RendezVousServer>();

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("MegabonkTogether.Server")
            .AddPrometheusExporter();
    });

builder.Configuration.GetSection("Config").Get<ConfigOptions>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear(); //This should be fine as long as we are behind a trusted proxy (the server run behind a nginx)
    options.KnownProxies.Clear(); //You absolute want to remove these two lines if you are modifying this for you and not using a proxy
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
});

var app = builder.Build();

app.UseForwardedHeaders();

app.UseWebSockets();

app.MapPrometheusScrapingEndpoint("/metrics");

_ = app.Services.GetRequiredService<IRendezVousServer>(); // Initialize UDP server
_ = app.Services.GetRequiredService<IMetricsService>(); // Initialize Metrics service

app.MapWhen(context => context.Request.Path.StartsWithSegments("/ws"), wsApp =>
{
    wsApp.Run(async context =>
    {
        var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        if (context.WebSockets.IsWebSocketRequest)
        {
            using var cts = new CancellationTokenSource();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();

            var query = context.Request.Query;

            var enabledSharedExperience = bool.Parse(query["enabledSharedExperience"].ToString());
            bool.TryParse(query["enabledSharedKills"].ToString(), out var enabledSharedKills); // Optional: older clients don't send it

            // Optional, host decided difficulty multipliers, one query parameter per enemy stat. Invariant culture: the plugin
            // always sends "1.3", never "1,3". Absent or malformed -> default, out of range -> clamped
            var difficulty = new DifficultySettings();
            foreach (var stat in DifficultyRange.Stats)
            {
                if (float.TryParse(query[DifficultyRange.QueryName(stat)].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMultiplier))
                {
                    difficulty.Set(stat, DifficultyRange.Normalize(stat, parsedMultiplier));
                }
            }

            if (query.ContainsKey("random"))
            {
                await handler.HandleRandomClientAsync(webSocket,
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Connection.RemotePort,
                    enabledSharedExperience,
                    enabledSharedKills,
                    difficulty,
                    cts.Token);
            }
            else if (query.ContainsKey("friendlies"))
            {
                var role = Enum.Parse<Role>(query["role"].ToString());
                var code = query["code"].ToString();
                var name = query["name"].ToString();

                await handler.HandleFriendliesClientAsync(webSocket,
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Connection.RemotePort,
                    role,
                    code,
                    name,
                    enabledSharedExperience,
                    enabledSharedKills,
                    difficulty,
                    cts.Token);
            }
            else
            {
                logger.LogWarning("Unknown connection mode");
                context.Response.StatusCode = 400;
            }
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    });
});

await app.RunAsync();