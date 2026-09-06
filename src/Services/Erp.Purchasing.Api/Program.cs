using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
Log.Information("Starting Erp.Purchasing.Api...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .WriteTo.Console()
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(ctx.Configuration));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["IdentityServer:Authority"];
            options.Audience = "purchasing-api";
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        });

    builder.Services.AddAuthorization();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/health", () => Results.Ok(new { service = "Purchasing.Api", status = "healthy" }))
       .AllowAnonymous();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Failure during Purchasing.Api startup");
}
finally
{
    Log.CloseAndFlush();
}
