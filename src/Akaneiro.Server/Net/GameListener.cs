using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Akaneiro.Server.Game;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akaneiro.Server.Net
{
    /// <summary>Accepts game client connections on the game port.</summary>
    public sealed class GameListener : BackgroundService
    {
        private readonly World _world;
        private readonly ServerOptions _options;
        private readonly ILoggerFactory _loggers;
        private readonly ILogger _log;

        public GameListener(World world, IOptions<ServerOptions> options, ILoggerFactory loggers)
        {
            _world = world;
            _options = options.Value;
            _loggers = loggers;
            _log = loggers.CreateLogger("Game");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new TcpListener(IPAddress.Any, _options.GamePort);
            listener.Start();
            _log.LogInformation("Game server listening on port {Port}", _options.GamePort);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync(stoppingToken);
                    _ = HandleClientAsync(client, stoppingToken);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            var connection = new Connection(client, _log);
            var session = new Session(_world, connection, _loggers.CreateLogger($"Session#{connection.Id}"));
            _log.LogInformation("Client #{Id} connected from {Remote}", connection.Id, connection.RemoteEndPoint);
            try
            {
                await connection.RunAsync(session, ct);
            }
            finally
            {
                session.OnDisconnected();
                _log.LogInformation("Client #{Id} disconnected", connection.Id);
            }
        }
    }
}
