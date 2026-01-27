using CbrRatesLoader;
using CbrRatesLoader.Data;
using CbrRatesLoader.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var cli = CommandLineOptions.Parse(args);

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? "Host=postgres;Port=5432;Database=cbr_rates;Username=postgres;Password=postgres";

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

var cbrEndpoint =
    builder.Configuration["CBR__Endpoint"]
    ?? builder.Configuration["CBR_ENDPOINT"]
    ?? "https://www.cbr.ru/DailyInfoWebServ/DailyInfo.asmx";

builder.Services.AddHttpClient<CbrSoapClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("task_corteos/1.0 (+console app)");
});

builder.Services.AddSingleton(new AppConfig(connectionString, cbrEndpoint));
builder.Services.AddSingleton(cli);
builder.Services.AddSingleton<DatabaseMigrator>();
builder.Services.AddSingleton<RateImporter>();
builder.Services.AddSingleton<AppRunner>();

var app = builder.Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await app.Services.GetRequiredService<AppRunner>().RunAsync(cts.Token);
