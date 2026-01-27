using System.Globalization;

namespace CbrRatesLoader;

public sealed record CommandLineOptions(
    bool Daemon,
    DateOnly? From,
    DateOnly? To,
    int BackfillDays,
    TimeOnly RunAtLocalTime,
    string TimeZoneId)
{
    public static CommandLineOptions Parse(string[] args)
    {
        // Минимальный парсер без сторонних зависимостей.
        // Поддерживаемые аргументы:
        // --daemon / --once
        // --from=yyyy-MM-dd
        // --to=yyyy-MM-dd
        // --backfillDays=30
        // --runAt=HH:mm
        // --tz=Europe/Moscow (Linux) или Russian Standard Time (Windows)

        var daemon = args.Any(a => string.Equals(a, "--daemon", StringComparison.OrdinalIgnoreCase));
        if (args.Any(a => string.Equals(a, "--once", StringComparison.OrdinalIgnoreCase)))
        {
            daemon = false;
        }

        var from = TryGetDate(args, "--from");
        var to = TryGetDate(args, "--to");

        var backfillDays = TryGetInt(args, "--backfillDays") ?? 30;
        var runAt = TryGetTime(args, "--runAt") ?? new TimeOnly(2, 0);
        var tz = TryGetString(args, "--tz") ?? "Europe/Moscow";

        return new CommandLineOptions(
            Daemon: daemon,
            From: from,
            To: to,
            BackfillDays: backfillDays,
            RunAtLocalTime: runAt,
            TimeZoneId: tz);
    }

    private static DateOnly? TryGetDate(string[] args, string key)
    {
        var raw = TryGetString(args, key);
        if (raw is null) return null;

        return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static TimeOnly? TryGetTime(string[] args, string key)
    {
        var raw = TryGetString(args, key);
        if (raw is null) return null;

        return TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t
            : null;
    }

    private static int? TryGetInt(string[] args, string key)
    {
        var raw = TryGetString(args, key);
        if (raw is null) return null;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
    }

    private static string? TryGetString(string[] args, string key)
    {
        var prefix = key + "=";
        var match = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : match[prefix.Length..].Trim();
    }
}

