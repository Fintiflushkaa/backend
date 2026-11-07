using Domain.Entities.Circadian;
using Domain.Entities.User;
using CircadianWindow = Domain.Entities.Circadian.CircadianWindow;

namespace Infrastructure.Services;

public static class CircadianCalculator
{
    public static List<CircadianCurve> Calculate(
        SleepProfile sleep, RhythmPreferences preferences,
        DateOnly date, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(sleep);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(timeZone);
        var msfSc = CalculateMsfSc(sleep);
        if (!Enum.IsDefined(preferences.AfternoonDip))
            throw new ArgumentException("Некорректный ответ о времени спада");

        var schedule = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? sleep.FreeDays : sleep.WorkDays;
        var wake = schedule.WakeTime.ToTimeSpan().TotalHours;
        var bed = schedule.SleepStart.ToTimeSpan().TotalHours;
        if (bed <= wake) bed += 24;

        var dip = preferences.AfternoonDip switch
        {
            DipPreference.From12To14 => 13.0,
            DipPreference.From14To16 => 15.0,
            DipPreference.From16To18 => 17.0,
            DipPreference.After18 => 19.0,
            _ => wake + 7
        };
        dip += 24 * Math.Round(((wake + bed) / 2 - dip) / 24);
        var hasDip = preferences.AfternoonDip != DipPreference.NoNoticeableDip;

        Parameters[] categories =
        [
            new(EnergyCurveKind.General,  .55, .55, .20, .40, -3,   preferences.GeneralPeak),
            new(EnergyCurveKind.Mental,   .50, .55, .25, .50, -5,   preferences.MentalPeak),
            new(EnergyCurveKind.Physical, .90, .20, .05, .20, -.5,  preferences.PhysicalPeak),
            new(EnergyCurveKind.Social,   .60, .25, .10, .20,  .5,  preferences.SocialPeak),
            new(EnergyCurveKind.Routine,  .40, .30, .05, .20, -2.5, preferences.RoutinePeak)
        ];
        var times = new List<double>();
        for (var i = 0; wake + i * .25 < bed - 1e-9; i++) times.Add(wake + i * .25);
        times.Add(bed);

        DateTimeOffset At(double hour)
        {
            var local = DateTime.SpecifyKind(
                date.ToDateTime(TimeOnly.MinValue).AddTicks(
                    (long)Math.Round(hour * TimeSpan.TicksPerHour)), DateTimeKind.Unspecified);
            if (timeZone.IsInvalidTime(local) || timeZone.IsAmbiguousTime(local))
                throw new ArgumentException("Расчёт для интервала перевода часов пока не поддерживается");
            return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
        }
        var timestamps = times.Select(At).ToArray();
        var curves = new List<CircadianCurve>();

        foreach (var p in categories)
        {
            double Raw(double t)
            {
                var elapsed = t - wake;
                var pressure = 1 - (1 - .15) * Math.Exp(-elapsed / 18.2);
                var phase = msfSc + 12.5 + p.Shift;
                return p.C * Math.Cos(2 * Math.PI * (t - phase) / 24)
                    - p.S * pressure
                    - (hasDip ? p.D : 0) * Math.Exp(-Math.Pow(t - dip, 2) / 2)
                    - p.I * Math.Exp(-elapsed / .6);
            }

            var raw = times.Select(Raw).ToArray();
            var modelPeak = times[Array.IndexOf(raw, raw.Max())];
            double? selfPeak = p.Preference switch
            {
                PeakPreference.Morning => 8,
                PeakPreference.AroundNoon => 12,
                PeakPreference.Afternoon => 16,
                PeakPreference.Evening => 20,
                PeakPreference.Night => 24,
                PeakPreference.NoDifference => null,
                _ => throw new ArgumentException("Некорректный ответ о пике энергии")
            };

            var difference = selfPeak.HasValue ? Mod24(selfPeak.Value - modelPeak + 12) - 12 : 0;
            var shift = Math.Clamp(.5 * difference, -3, 3);

            var values = times.Select(t => Raw(Math.Clamp(t - shift, wake, bed))).ToArray();
            var min = values.Min();
            var range = values.Max() - min;
            var points = values.Select((v, i) => new EnergyPoint(
                timestamps[i], range < 1e-12 ? .5 : Math.Clamp((v - min) / range, 0, 1))).ToArray();

            curves.Add(new CircadianCurve(p.Kind, points, BuildWindows(points)));
        }
        return curves;
    }

    public static double CalculateMsfSc(SleepProfile sleep)
    {
        double Duration(SleepSchedule schedule)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            var duration = Mod24((schedule.WakeTime.ToTimeSpan() - schedule.SleepStart.ToTimeSpan()).TotalHours);
            if (duration == 0) throw new ArgumentException("Время сна и пробуждения не должны совпадать");
            return duration;
        }
        var work = Duration(sleep.WorkDays);
        var free = Duration(sleep.FreeDays);
        var midpoint = sleep.FreeDays.SleepStart.ToTimeSpan().TotalHours + free / 2;
        var week = (5 * work + 2 * free) / 7;
        return Mod24(midpoint - (free > work ? (free - week) / 2 : 0));
    }

    private static IReadOnlyList<CircadianWindow> BuildWindows(EnergyPoint[] points)
    {
        var windows = new List<CircadianWindow>();
        for (var i = 1; i < points.Length; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            var cuts = new List<double> { 0, 1 };
            foreach (var threshold in new[] { .4, .9 })
                if ((a.Energy < threshold && b.Energy > threshold) ||
                    (a.Energy > threshold && b.Energy < threshold))
                    cuts.Add((threshold - a.Energy) / (b.Energy - a.Energy));
            cuts.Sort();
            DateTimeOffset At(double fraction) => a.Time + TimeSpan.FromTicks(
                (long)Math.Round((b.Time - a.Time).Ticks * fraction));

            for (var j = 1; j < cuts.Count; j++)
            {
                var start = At(cuts[j - 1]);
                var end = At(cuts[j]);
                if (end <= start) continue;
                var energy = a.Energy + (b.Energy - a.Energy) * (cuts[j - 1] + cuts[j]) / 2;
                var zone = energy >= .9 ? EnergyZone.Peak : energy <= .4 ? EnergyZone.Low : EnergyZone.Normal;
                if (windows.Count > 0 && windows[^1].Zone == zone)
                    windows[^1] = windows[^1] with { End = end };
                else windows.Add(new CircadianWindow(start, end, zone));
            }
        }
        return windows;
    }

    private static double Mod24(double value) => (value % 24 + 24) % 24;

    private sealed record Parameters(EnergyCurveKind Kind,
        double C, double S, double D, double I, double Shift, PeakPreference Preference);
}
