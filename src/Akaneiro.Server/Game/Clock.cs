using System;
using System.Collections.Generic;
using System.Threading;

namespace Akaneiro.Server.Game
{
    /// <summary>
    /// Where game logic gets the time from and how it runs things periodically. The server uses
    /// the real clock; tests use a clock they advance themselves, so a minute of monster fighting
    /// takes milliseconds and never depends on how fast the machine is.
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }

        /// <summary>Calls <paramref name="tick"/> every <paramref name="interval"/> until disposed.</summary>
        IDisposable Every(TimeSpan interval, Action tick);

        /// <summary>Calls <paramref name="action"/> once after <paramref name="delay"/> (unless disposed first).</summary>
        IDisposable After(TimeSpan delay, Action action);
    }

    public sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();

        public DateTime UtcNow => DateTime.UtcNow;

        public IDisposable Every(TimeSpan interval, Action tick) => new Timer(_ => tick(), null, interval, interval);

        // One-shot timers have to stay referenced until they fire, or the GC may collect them.
        private readonly HashSet<Timer> _pending = new HashSet<Timer>();

        public IDisposable After(TimeSpan delay, Action action)
        {
            Timer timer = null;
            timer = new Timer(_ =>
            {
                lock (_pending)
                    _pending.Remove(timer);
                timer.Dispose();
                action();
            }, null, Timeout.Infinite, Timeout.Infinite);
            lock (_pending)
                _pending.Add(timer);
            timer.Change(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, Timeout.InfiniteTimeSpan);
            return timer;
        }
    }
}
