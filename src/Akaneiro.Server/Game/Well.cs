using System;
using Akaneiro.Server.Protocol;
using Microsoft.Extensions.Logging;

namespace Akaneiro.Server.Game
{
    /// <summary>
    /// The village well (NPC_Well): it fills with karma over time and can be emptied when full.
    /// The client works out how full it is from GetWellData (level and when it began filling) and
    /// the server time; the tables Well.Quality (karma per filling) and Well.Size (minutes to fill)
    /// give 100 karma per 15 minutes on level 1. Upgrades were bought with crystals and are not
    /// offered by this client, so the well stays on level 1.
    /// </summary>
    public sealed class Well
    {
        public const int Level = 1;
        public const int KarmaPerFilling = 100;          // Well.Quality level 1 "PerHour"
        public static readonly TimeSpan FillTime = TimeSpan.FromMinutes(15); // Well.Size level 1 "Mins"

        private readonly Session _s;

        public Well(Session session) => _s = session;

        private long Now => new DateTimeOffset(_s.World.Clock.UtcNow, TimeSpan.Zero).ToUnixTimeSeconds();

        public void SendData()
        {
            if (_s.Account.WellBeginUnix == 0)
            {
                _s.Account.WellBeginUnix = Now;
                _s.SaveAccount();
            }
            var data = Proto.New<SWellData>();
            data.speedLevel = Level;
            data.timeLevel = Level;
            data.lidLevel = Level;
            data.beginTime = _s.Account.WellBeginUnix;
            _s.Send(GameResponses.GetWellDataResult(data, (ulong)Now));
        }

        public void Collect()
        {
            if (_s.Account.WellBeginUnix == 0 || Now - _s.Account.WellBeginUnix < (long)FillTime.TotalSeconds)
            {
                _s.Send(GameResponses.GetWellKarmaResult(Proto.Error(EServerErrorType.eUnknownError)));
                return;
            }
            _s.Account.WellBeginUnix = Now;
            _s.Account.Karma += KarmaPerFilling;
            _s.SaveAccount();
            _s.Log.LogInformation("   well gave {Karma} karma (total {Total})", KarmaPerFilling, _s.Account.Karma);
            _s.Send(GameResponses.GetWellKarmaResult(Proto.Error(EServerErrorType.eSuccess)));
            _s.SendMoney();
        }
    }
}
