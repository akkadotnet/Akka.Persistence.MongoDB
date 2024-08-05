using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Akka.Persistence.MongoDb.Hosting;
using LargeSnapshot.Actors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var hostBuilder = Host.CreateDefaultBuilder(args);
hostBuilder
    .ConfigureLogging(builder =>
    {
        builder.ClearProviders();
        builder.AddConsole();
        builder.Services.Configure<LoggerFilterOptions>(opt =>
        {
            opt.MinLevel = LogLevel.Debug;
        });
    })
    .ConfigureServices((context, services) =>
    {
        services.AddAkka("LargeSnapshotSys", builder =>
        {
            var snapshotOptions = new MongoDbGridFsSnapshotOptions()
            {
                ConnectionString = context.Configuration["ConnectionStrings:MongoDb"],
                AutoInitialize = true
            };

            builder
                .ConfigureLoggers(logger =>
                {
                    logger.ClearLoggers();
                    logger.AddDefaultLogger();
                })
                .WithSnapshot(snapshotOptions)
                .WithInMemoryJournal()
                .WithActors((system, registry) =>
                {
                    system.ActorOf(Props.Create(() => new PersistentActor("persisted")), "persisted-actor");
                });
        });
    });

await hostBuilder
    .UseConsoleLifetime()
    .RunConsoleAsync();