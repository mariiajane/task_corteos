namespace CbrRatesLoader;

public sealed record AppConfig(string PostgresConnectionString, string CbrEndpoint);

