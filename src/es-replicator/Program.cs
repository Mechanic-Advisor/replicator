using System;
using System.Diagnostics;
using System.Reflection;
using es_replicator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Grpc", LogEventLevel.Error)
    .Enrich.FromLogContext();
    // .WriteTo.File("log.log");

logConfig = environment?.ToLower() == "development"
    ? logConfig.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <s:{SourceContext}>;{NewLine}{Exception}")
    : logConfig.WriteTo.Console(new RenderedCompactJsonFormatter());
Log.Logger = logConfig.CreateLogger();

try {
    var fileInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
    Log.Information("Starting replicator {Version}", fileInfo.ProductVersion);
    
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            webBuilder => {
                webBuilder.UseSerilog();
                webBuilder.UseStartup<Startup>();
            }
        )
        .Build()
        .Run();
    return 0;
}
catch (Exception ex) {
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally {
    Log.CloseAndFlush();
}
