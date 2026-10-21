using Akaneiro.Server.Game;

namespace Akaneiro.Server.Tests.Harness;

/// <summary>
/// A clock that only moves when the test says so. Periodic work (the monster AI) runs at exactly
/// the moments it would have run in real time, so waiting a minute of game time takes milliseconds.
/// </summary>
public sealed class ManualClock : IClock
{
    private readonly List<Schedule> _schedules = new();

    public DateTime UtcNow { get; private set; } = new DateTime(2013, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    public IDisposable Every(TimeSpan interval, Action tick)
    {
        var schedule = new Schedule(this, interval, tick, UtcNow + interval);
        _schedules.Add(schedule);
        return schedule;
    }

    public IDisposable After(TimeSpan delay, Action action)
    {
        Schedule schedule = null;
        schedule = new Schedule(this, TimeSpan.Zero, () =>
        {
            schedule.Dispose();
            action();
        }, UtcNow + (delay < TimeSpan.Zero ? TimeSpan.Zero : delay));
        _schedules.Add(schedule);
        return schedule;
    }

    public void Advance(TimeSpan span)
    {
        DateTime end = UtcNow + span;
        while (true)
        {
            Schedule next = _schedules.Where(s => s.Due <= end).OrderBy(s => s.Due).FirstOrDefault();
            if (next == null)
                break;
            UtcNow = next.Due;
            next.Due += next.Interval; // a one-shot (interval 0) removes itself in its tick
            next.Tick();
        }
        UtcNow = end;
    }

    private sealed class Schedule : IDisposable
    {
        private readonly ManualClock _clock;

        public Schedule(ManualClock clock, TimeSpan interval, Action tick, DateTime due)
        {
            _clock = clock;
            Interval = interval;
            Tick = tick;
            Due = due;
        }

        public TimeSpan Interval { get; }
        public Action Tick { get; }
        public DateTime Due { get; set; }

        public void Dispose() => _clock._schedules.Remove(this);
    }
}
