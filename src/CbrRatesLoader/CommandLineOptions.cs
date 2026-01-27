namespace CbrRatesLoader;

public sealed record CommandLineOptions(
    bool Daemon, 
    int BackfillDays, 
    string TimeZoneId)
{
    public static CommandLineOptions Parse(string[] args)
    {
        // Проверяем флаг --daemon
        var daemon = args.Any(a => string.Equals(a, "--daemon", StringComparison.OrdinalIgnoreCase));

        // Получаем кол-во дней для начальной загрузки (по умолчанию 30)
        var backfillDays = TryGetInt(args, "--days") ?? 30;

        // Таймзона (Europe/Moscow для Linux/Docker или Russian Standard Time для Windows)
        var tz = TryGetString(args, "--tz") ?? "Europe/Moscow";

        return new CommandLineOptions(daemon, backfillDays, tz);
    }

    private static int? TryGetInt(string[] args, string key)
    {
        var raw = TryGetString(args, key);
        return int.TryParse(raw, out var result) ? result : null;
    }

    private static string? TryGetString(string[] args, string key)
    {
        var prefix = key + "=";
        var match = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : match[prefix.Length..].Trim();
    }
}
