using System;
using Akaneiro.Server.Data;
using Microsoft.Extensions.Options;

namespace Akaneiro.Server.Game
{
    /// <summary>
    /// Shared server state. All game logic runs while holding <see cref="Sync"/>, so handlers
    /// never race each other (the server is meant for a handful of local players at most).
    /// </summary>
    public sealed class World
    {
        public object Sync { get; } = new object();
        public ServerOptions Options { get; }
        public AccountStore Accounts { get; }
        public GameData Data { get; }

        /// <summary>Time source of all game logic (cooldowns, monster behaviour, ability objects).</summary>
        public IClock Clock { get; init; } = SystemClock.Instance;

        /// <summary>Dice of all game logic (damage, loot, monster attacks); only used under <see cref="Sync"/>.</summary>
        public Random Random { get; init; } = new Random();

        public World(IOptions<ServerOptions> options, AccountStore accounts, GameData data)
        {
            Options = options.Value;
            Accounts = accounts;
            Data = data;
        }
    }
}
