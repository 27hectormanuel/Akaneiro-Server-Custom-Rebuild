using System;
using Akaneiro.Server.Protocol;
using Microsoft.Extensions.Logging;

namespace Akaneiro.Server.Game
{
    /// <summary>
    /// Kill chains: monsters killed in quick succession count as a chain, and a long enough chain
    /// pays bonus experience when it ends ("GOOD!", "LETHAL!", "SLAYER!").
    ///
    /// The client counts the kills itself on every ObjectLeave once the server has started the
    /// chain (UpdateSerialKillNum), and shows the rank and bonus of SerialKillOver. The original
    /// thresholds and bonus live in lost server code; the recordings show "GOOD! +18 XP" after three
    /// wolves (12 XP each) and "LETHAL! +54 XP" after six, which fits half and three quarters of the
    /// chain's experience.
    /// </summary>
    public sealed class KillChain
    {
        /// <summary>Seconds after the last kill until the chain is over and its rank shows (assumed;
        /// the rank only appears once the chain has ended, so a short window keeps it close to the kills).</summary>
        public const double WindowSeconds = 3;

        public const int Good = 3;
        public const int Lethal = 6;
        public const int Slayer = 10;

        private readonly Session _s;
        private int _count;
        private ulong _xp;
        private bool _started;
        private DateTime _lastKill;

        public KillChain(Session session) => _s = session;

        public int Count => _count;

        /// <summary>
        /// Counts monsters that are about to leave (call before sending their ObjectLeave, so the
        /// client already knows the chain when it counts them).
        /// </summary>
        public void OnKills(int kills, ulong xp)
        {
            if (kills <= 0)
                return;

            DateTime now = _s.World.Clock.UtcNow;
            int before = _count;
            _count += kills;
            _xp += xp;
            _lastKill = now;

            if (!_started && _count >= 2)
            {
                _started = true;
                _s.Send(BattleResponses.UpdateSerialKillNum(before + 1));
            }

            SceneInstance scene = _s.Scene;
            scene?.Schedule(now.AddSeconds(WindowSeconds), () =>
            {
                if (_s.World.Clock.UtcNow >= _lastKill.AddSeconds(WindowSeconds))
                    End();
            });
        }

        /// <summary>Bonus experience of a chain of <paramref name="kills"/> worth <paramref name="xp"/>.</summary>
        public static ulong Bonus(int kills, ulong xp) =>
            kills >= Slayer ? xp
            : kills >= Lethal ? xp * 3 / 4
            : kills >= Good ? xp / 2
            : 0;

        /// <summary>Ends the chain: the client shows the rank, the bonus goes to the player.</summary>
        public void End()
        {
            if (_count == 0)
                return;

            int kills = _count;
            ulong bonus = Bonus(kills, _xp);
            bool started = _started;
            _count = 0;
            _xp = 0;
            _started = false;

            if (!started)
                return;
            _s.Send(BattleResponses.SerialKillOver(kills, (int)bonus));
            if (bonus > 0)
            {
                _s.Log.LogInformation("   kill chain of {Kills} (+{Bonus} xp)", kills, bonus);
                _s.Battle.GrantExperience(bonus);
            }
        }
    }
}
