using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Infrastructure;
using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Core;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.ExecutiveReporting;
using PMGM.Api.Modules.GrandArchive;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.InstitutionalCalendar;
using PMGM.Api.Modules.InstitutionalProjections;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Notifications;
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

builder.Services.Configure<DocumentStorageOptions>(
    builder.Configuration.GetSection(DocumentStorageOptions.SectionName));
builder.Services.Configure<DocumentMalwareOptions>(
    builder.Configuration.GetSection(DocumentMalwareOptions.SectionName));

var mainConnectionString = builder.Configuration.GetConnectionString("MainDatabase")
    ?? "Host=localhost;Port=5432;Database=pmgm;Username=pmgm_app;Password=pmgm_dev_only";

builder.Services.AddScoped<CalendarSourceProjectionInterceptor>();
builder.Services.AddDbContext<PmgmDbContext>((services, options) =>
    options.UseNpgsql(mainConnectionString)
        .AddInterceptors(services.GetRequiredService<CalendarSourceProjectionInterceptor>()));
builder.Services.AddDbContext<BootstrapDbContext>(options => options.UseNpgsql(mainConnectionString));
builder.Services.AddDbContext<GrandSecretariatDbContext>(options => options.UseNpgsql(mainConnectionString));
builder.Services.AddDbContext<LodgeManagementDbContext>((services, options) =>
    options.UseNpgsql(mainConnectionString)
        .AddInterceptors(services.GetRequiredService<CalendarSourceProjectionInterceptor>()));
builder.Services.AddDbContext<DocumentManagementDbContext>(options => options.UseNpgsql(mainConnectionString));
builder.Services.AddDbContext<GrandArchiveDbContext>(options => options.UseNpgsql(mainConnectionString));
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(mainConnectionString));
builder.Services.AddDbContext<CalendarDbContext>(options => options.UseNpgsql(mainConnectionString));
builder.Services.AddDbContext<RegimenInteriorDbContext>(options => options.UseNpgsql(mainConnectionString));
builder.Services.AddDbContext<CandidateIntakeDbContext>(options => options.UseNpgsql(mainConnectionString));
builder.Services.AddDbContext<AdmissionsDbContext>(options => options.UseNpgsql(mainConnectionString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool?>("Authentication:RequireHttpsMetadata")
            ?? !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IInstitutionalAccessService, InstitutionalAccessService>();
builder.Services.AddScoped<IInstitutionalMemberContextResolver, InstitutionalMemberContextResolver>();
builder.Services.AddScoped<IInstitutionalBootstrapService, InstitutionalBootstrapService>();
builder.Services.AddSingleton<ICeremonyEligibilityService, CeremonyEligibilityService>();
builder.Services.AddSingleton<IDocumentObjectStore, S3DocumentObjectStore>();
builder.Services.AddSingleton<IDocumentMalwareScanner, ClamAvDocumentMalwareScanner>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IPrivacyLegalRuleResolver, PrivacyLegalRuleResolver>();
builder.Services.AddScoped<IInstitutionalNotificationService, InstitutionalNotificationService>();
builder.Services.AddScoped<IInstitutionalCalendarProjectionService, InstitutionalCalendarProjectionService>();
builder.Services.AddScoped<IInstitutionalCalendarSourceSyncService, InstitutionalCalendarSourceSyncService>();
builder.Services.AddScoped<IExecutiveReportingService, ExecutiveReportingService>();
builder.Services.AddScoped<IRegimenInteriorMemberControlService, RegimenInteriorMemberControlService>();
builder.Services.AddScoped<IRegimenInteriorDataQualityService, RegimenInteriorDataQualityService>();
builder.Services.AddScoped<IDataQualityCaseService, DataQualityCaseService>();
builder.Services.AddScoped<IGrandArchiveService, GrandArchiveService>();
builder.Services.AddScoped<FirstImplementationSeedService>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
    await db.Database.MigrateAsync();
    var bootstrapDb = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
    await bootstrapDb.Database.MigrateAsync();
    var documentDb = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
    await documentDb.Database.MigrateAsync();
    var grandArchiveDb = scope.ServiceProvider.GetRequiredService<GrandArchiveDbContext>();
    await grandArchiveDb.Database.MigrateAsync();
    var notificationDb = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await notificationDb.Database.MigrateAsync();
    var calendarDb = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
    await calendarDb.Database.MigrateAsync();
    var regimenInteriorDb = scope.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>();
    await regimenInteriorDb.Database.MigrateAsync();
    var candidateIntakeDb = scope.ServiceProvider.GetRequiredService<CandidateIntakeDbContext>();
    await candidateIntakeDb.Database.MigrateAsync();
    var admissionsDb = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>();
    await admissionsDb.Database.MigrateAsync();
}

if (builder.Configuration.GetValue<bool>("DemoData:Enabled"))
{
    if (app.Environment.IsProduction())
        throw new InvalidOperationException("DemoData:Enabled nunca puede utilizarse en Production.");

    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<FirstImplementationSeedService>().SeedAsync();
}

app.UseExceptionHandler();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseMiddleware<LibraryDegreeAccessMiddleware>();
app.UseAuthorization();
app.UseMiddleware<CandidatePublishedLockMiddleware>();
app.UseMiddleware<CandidateInitiationAuthorizationGuardMiddleware>();
app.UseMiddleware<GrandMasterCeremonyAuthorizationGuardMiddleware>();

var apiVersion = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    .Split('+', 2)[0]
    ?? "0.0.0-unknown";

app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "PMGM.Api" }));
app.MapGet("/health/ready", async (PmgmDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready", database = "postgresql" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapGet("/api/system/info", () => Results.Ok(new
{
    project = "Proyecto Centenario — Modernización Gran Logia Mixta de Chile",
    api = "PMGM.Api",
    version = apiVersion,
    runtime = ".NET 10",
    culture = "es-CL",
    institutionalTimeZone = "America/Santiago",
    defaultCurrency = "CLP"
}));

app.MapSessionEndpoints();
app.MapBootstrapEndpoints();
app.MapOrganizationEndpoints();
app.MapMembershipEndpoints();
app.MapMemberSelfEndpoints();
app.MapTransferEndpoints();
app.MapRegimenInteriorEndpoints();
app.MapRegimenInteriorMemberControlEndpoints();
app.MapRegimenInteriorDataQualityEndpoints();
app.MapDataQualityCaseEndpoints();
app.MapExecutiveReportingEndpoints();
app.MapTreasuryEndpoints();
app.MapHospitalariaEndpoints();
app.MapInstitutionalRegularityProjectionEndpoints();
app.MapCandidateIntakeEndpoints();
app.MapCandidateWorkshopIntakeEndpoints();
app.MapCandidateWorkflowEndpoints();
app.MapAdmissionEndpoints();
app.MapCeremonyEndpoints();
app.MapGrandMasterCeremonyEndpoints();
app.MapCandidatePublicationEndpoints();
app.MapCeremonyReviewQueueEndpoints();
app.MapGrandSecretariatEndpoints();
app.MapGrandSecretariatQueryEndpoints();
app.MapGrandSecretariatCeremonyQueueEndpoints();
app.MapLodgeManagementEndpoints();
app.MapLodgeInstructionEndpoints();
app.MapDocumentManagementEndpoints();
app.MapDocumentManagementQueryEndpoints();
app.MapDocumentContentEndpoints();
app.MapDocumentContentRecoveryEndpoints();
app.MapLibraryCatalogEndpoints();
app.MapLibraryAccessPolicyEndpoints();
app.MapLibraryCatalogMetadataEndpoints();
app.MapGrandArchiveEndpoints();
app.MapNotificationEndpoints();
app.MapInstitutionalCalendarEndpoints();
app.MapInstitutionalCalendarSourceEndpoints();
app.MapPrivacyEndpoints();
app.MapPrivacyRetentionEndpoints();
app.MapPrivacyRiskEndpoints();
app.MapPrivacyProcessorEndpoints();
app.MapPrivacyProcessorLifecycleEndpoints();
app.MapPrivacyLegalRuleEndpoints();
app.MapPrivacyWorkflowEndpoints();

app.Run();

public partial class Program;
