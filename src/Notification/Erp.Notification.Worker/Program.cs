using Erp.Common.Configuration;
using Erp.Notification.Application;
using Erp.Notification.Storage;
using Erp.Notification.Worker;
using Erp.Storage;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddInfisicalSecrets(builder.Environment);

var requiredSettings = new List<string> { "ConnectionStrings:ErpDb" };
if (!builder.Environment.IsDevelopment())
    requiredSettings.AddRange([
        "Smtp:Host",
        "Smtp:UserName",
        "Smtp:Password",
        "Smtp:FromEmail"
    ]);
builder.Configuration.EnsureConfigured([.. requiredSettings]);

builder.Services.AddSerilog((services, lc) => lc
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(builder.Configuration));

// The worker only drains the email queue, but the context is the same one the API uses: the
// modules share a database, and now a model.
builder.Services.AddStorage(builder.Configuration);
builder.Services.AddNotificationStorage();
builder.Services.AddNotificationApplication(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
