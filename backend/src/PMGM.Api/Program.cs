using System.Globalization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Core;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.InstitutionalProjections;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Privacy;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.Treasury;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("es-CL") };
    options.DefaultRequestCulture = new RequestCulture("es-CL");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

var mainConnectionString = builder.Configuration.GetConnectionString("MainDatabase")
    ?? "Host=localhost;Port=5432;Database=pmgm;Username=pmgm_app;Password=pmgm_dev_only";

builder.Services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(mainConnectionString));
builder.Services.AddDbContext<GrandSecretariatDbContext>(options => options.UseNpgsql(mainConnectionString));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IInstitutionalAccessService, InstitutionalAccessService>();
builder.Services.AddSingleton<ICeremonyEligibilityService, CeremonyEligibilityService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IPrivacyLegalRuleResolver, PrivacyLegalRuleResolver>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

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
    version = "0.11.1",
    runtime = ".NET 10",
    culture = "es-CL",
    institutionalTimeZone = "America/Santiago",
    defaultCurrency = "CLP"
}));

app.MapSessionEndpoints();
app.MapOrganizationEndpoints();
app.MapMembershipEndpoints();
app.MapTransferEndpoints();
app.MapRegimenInteriorEndpoints();
app.MapTreasuryEndpoints();
app.MapHospitalariaEndpoints();
app.MapInstitutionalRegularityProjectionEndpoints();
app.MapCeremonyEndpoints();
app.MapCandidatePublicationEndpoints();
app.MapGrandSecretariatEndpoints();
app.MapPrivacyEndpoints();
app.MapPrivacyRetentionEndpoints();
app.MapPrivacyRiskEndpoints();
app.MapPrivacyProcessorEndpoints();
app.MapPrivacyProcessorLifecycleEndpoints();
app.MapPrivacyLegalRuleEndpoints();
app.MapPrivacyWorkflowEndpoints();

app.Run();

public partial class Program;
