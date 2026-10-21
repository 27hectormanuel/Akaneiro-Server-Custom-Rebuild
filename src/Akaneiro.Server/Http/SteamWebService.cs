using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akaneiro.Server.Http
{
    /// <summary>
    /// Replacement for the web service behind http://mw.angry-red.com used by the Steam build
    /// (see client Scripts/Steam/Steamworks.cs). Responses are JSON: {"success":{...}} or {"error":{"msg":...}}.
    /// </summary>
    public static class SteamWebService
    {
        public static void Map(WebApplication app)
        {
            ILogger log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Web");
            ServerOptions options = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value;

            // Steam ticket -> game server address. The ticket is not verified.
            app.MapPost("/client/steam/user/authorize", () =>
            {
                string token = Guid.NewGuid().ToString("N");
                string server = $"{options.PublicHost}:{options.GamePort}";
                log.LogInformation("Steam authorize -> {Server}", server);
                return Results.Json(new { success = new { token, server } });
            });

            app.MapPost("/client/steam/user/refresh", (HttpRequest request) =>
            {
                string token = request.HasFormContentType ? request.Form["token"].ToString() : "";
                return Results.Json(new { success = new { token } });
            });

            app.MapPost("/client/steam/dlc/validate", () => Results.Json(new { success = new { msg = "ok" } }));

            app.MapPost("/client/steam/transaction/init", () =>
                Results.Json(new { error = new { msg = "Purchases are not available on this private server." } }));

            app.MapPost("/client/steam/transaction/complete", () =>
                Results.Json(new { error = new { msg = "Purchases are not available on this private server." } }));

            // Everything else, including the crystal shop page the client opens in the Steam overlay
            // (Steamworks.ShowShop, /client/steam/shop), does not exist here.
            app.MapFallback((HttpRequest request) =>
            {
                log.LogWarning("Unhandled web request {Method} {Path}{Query}", request.Method, request.Path, request.QueryString);
                return Results.NotFound();
            });
        }
    }
}
