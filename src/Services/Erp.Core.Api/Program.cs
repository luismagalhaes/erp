using Erp.Core.Application;
using Erp.Core.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
Log.Information("Starting Erp.Core.Api...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .WriteTo.Console()
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(ctx.Configuration));

    builder.Services.AddCoreStorage(builder.Configuration);
    builder.Services.AddCoreApplication();

    builder.Services.AddControllers();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["IdentityServer:Authority"];
            options.Audience = "core-api";
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        });

    builder.Services.AddAuthorization();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "ERP Core API";
        });

        app.MapGet("/", () => Results.Redirect("/scalar"));
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Failure during Core.Api startup");
}
finally
{
    Log.CloseAndFlush();
}
