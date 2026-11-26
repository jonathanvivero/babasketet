using Babasketet.API.Api;
using Babasketet.API.Application;
using Babasketet.API.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var authOptions = builder.Configuration.GetSection(DeviceTokenOptions.SectionName).Get<DeviceTokenOptions>()
                  ?? new DeviceTokenOptions();
var deviceTokenService = new DeviceTokenService(authOptions);

builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton<IDeviceTokenService>(deviceTokenService);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddDataProtection();
builder.Services.AddScoped<ICallerContextAccessor, HttpCallerContextAccessor>();
builder.Services.AddScoped<IHouseNotifier, SignalRHouseNotifier>();
builder.Services.AddScoped<ISmtpDeliveryClient, MailKitSmtpDeliveryClient>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<UserDirectoryService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<HouseService>();
builder.Services.AddScoped<ShoppingService>();

builder.Services.AddDbContext<BabasketetDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BabasketetDb")));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

const string DualScheme = "DualAuth";
const string DeviceScheme = "DeviceBearer";
const string ZitadelScheme = "ZitadelBearer";

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = DualScheme;
        options.DefaultChallengeScheme = DualScheme;
    })
    .AddPolicyScheme(DualScheme, "Zitadel OIDC or Anonymous Device Token", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            var rawToken = authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                ? authHeader["Bearer ".Length..].Trim()
                : context.Request.Query["access_token"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(rawToken) && deviceTokenService.TryValidateToken(rawToken).HasValue)
            {
                return DeviceScheme;
            }

            return ZitadelScheme;
        };
    })
    .AddJwtBearer(DeviceScheme, options =>
    {
        options.TokenValidationParameters = deviceTokenService.CreateValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    })
    .AddJwtBearer(ZitadelScheme, options =>
    {
        options.Authority = authOptions.OidcAuthority;
        options.RequireHttpsMetadata = authOptions.OidcAuthority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (Uri.TryCreate(authOptions.OidcIssuer, UriKind.Absolute, out var issuerUri) &&
            Uri.TryCreate(authOptions.OidcAuthority, UriKind.Absolute, out var authorityUri) &&
            !string.Equals(issuerUri.Authority, authorityUri.Authority, StringComparison.OrdinalIgnoreCase))
        {
            options.BackchannelHttpHandler = new ZitadelBackchannelHandler(issuerUri, authorityUri);
        }
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = [authOptions.OidcIssuer, authOptions.OidcAuthority],
            ValidateAudience = !string.IsNullOrWhiteSpace(authOptions.OidcAudience),
            AudienceValidator = (audiences, _, _) =>
            {
                var valid = authOptions.GetValidAudiences();
                if (valid.Count == 0) return true;
                return audiences.Any(a => valid.Contains(a));
            },
            NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:8081")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BabasketetDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if ((path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs")) &&
        context.User.Identity?.IsAuthenticated == true)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        var rawToken = authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authHeader["Bearer ".Length..].Trim()
            : context.Request.Query["access_token"].FirstOrDefault();

        var userDir = context.RequestServices.GetRequiredService<UserDirectoryService>();
        await userDir.SyncAndValidateCallerAsync(rawToken, context.RequestAborted);
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
app.MapGet("/health/ready", async (BabasketetDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
}).AllowAnonymous();

app.MapBabasketetApi();

app.Run();

public partial class Program;
