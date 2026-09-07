using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Membership;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<PmgmDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("MainDatabase")
        ?? "Host=localhost;Port=5432;Database=pmgm;Username=pmgm_app;Password=pmgm_dev_only";

    options.UseNpgsql(connectionString);
});

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "ok",
    service = "PMGM.Api"
}));

app.MapGet("/health/ready", async (PmgmDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { status = "ready", database = "postgresql" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/api/system/info", () => Results.Ok(new
{
    project = "Proyecto Milenio — Modernización Gran Logia Mixta de Chile",
    api = "PMGM.Api",
    version = "0.2.0",
    runtime = ".NET 10"
}));

app.MapMembershipEndpoints();

app.Run();

public partial class Program;
