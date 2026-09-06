using Erp.Notification.Application;
using Erp.Notification.Storage;
using Erp.Notification.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, lc) => lc
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(builder.Configuration));

builder.Services.AddNotificationStorage(builder.Configuration);
builder.Services.AddNotificationApplication(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
