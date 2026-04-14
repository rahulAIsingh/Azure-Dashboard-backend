using System.Text;
using AzureFinOps.API.Data;
using AzureFinOps.API.Services;
using AzureFinOps.API.Utilities;
using AzureFinOps.API.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var serverUrl = builder.Configuration["Server:Url"] ?? "http://localhost:5038";
builder.WebHost.UseUrls(serverUrl);

// Add services to the container.
builder.Services.AddControllers();

// Register Hosted Services (Background Workers)
builder.Services.AddHostedService<AzureCostImportWorker>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});


// Add Infrastructure Services (DbContext, Repositories, etc.)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("Jwt Secret is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// Add Swagger Setup
builder.Services.AddSwaggerSetup();

var app = builder.Build();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    await DatabaseSeeder.SeedAsync(context);
}

var clearImportedCostDataOnce = app.Configuration.GetValue<bool>("StartupDataMaintenance:ClearImportedCostDataOnce");
if (clearImportedCostDataOnce)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("StartupDataMaintenance");
    var dbContext = services.GetRequiredService<ApplicationDbContext>();

    logger.LogWarning("StartupDataMaintenance:ClearImportedCostDataOnce is enabled. Clearing imported Azure cost data before serving requests.");
    await DatabaseSeeder.ClearAllDataAsync(dbContext);
    logger.LogWarning("Imported Azure cost data cleared. Disable StartupDataMaintenance:ClearImportedCostDataOnce after verifying the next import cycle.");
}

var manualPricingImportEnabled = app.Configuration.GetValue<bool>("ManualPricingImport:Enabled");
if (manualPricingImportEnabled)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("ManualPricingImport");
    var importService = services.GetRequiredService<IManualPricingImportService>();

    logger.LogWarning("ManualPricingImport:Enabled is true. Starting one-time pricing backfill import.");
    var summary = await importService.ImportAsync();
    logger.LogWarning(
        "Manual pricing backfill completed. Inserted={Inserted}, Unmatched={Unmatched}, Skipped={Skipped}, TotalImportedAmtInr={TotalImportedAmtInr}. Disable ManualPricingImport:Enabled after verification.",
        summary.MatchedResourcesInserted,
        summary.UnmatchedResources,
        summary.SkippedRows,
        summary.TotalImportedAmtInr);
}


// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure FinOps API v1"));

// app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
