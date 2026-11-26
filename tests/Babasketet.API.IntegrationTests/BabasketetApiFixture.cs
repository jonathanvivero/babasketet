using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Babasketet.API.Application;
using Babasketet.API.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace Babasketet.API.IntegrationTests;

public sealed class BabasketetApiFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private readonly RsaSecurityKey _testOidcKey;
    private readonly string _externalConnectionString =
        Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING") ?? string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;
    public FakeTimeProvider TimeProvider { get; } = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    public FakeSmtpDeliveryClient FakeSmtp { get; } = new();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public BabasketetApiFixture()
    {
        var rsa = RSA.Create(2048);
        _testOidcKey = new RsaSecurityKey(rsa) { KeyId = "test-zitadel-key" };
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_externalConnectionString))
        {
            _postgres = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("babasketet_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
            await _postgres.StartAsync();
            ConnectionString = _postgres.GetConnectionString();
        }
        else
        {
            ConnectionString = _externalConnectionString;
        }

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:BabasketetDb", ConnectionString);
                builder.UseSetting("Auth:DeviceTokenSigningKey", "integration-test-device-signing-key-32-chars!");
                builder.UseSetting("Auth:OidcIssuer", "https://test-zitadel.local");
                builder.UseSetting("Auth:OidcAuthority", "https://test-zitadel.local");
                builder.UseSetting("Auth:OidcAudience", "babasketet");

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(TimeProvider);
                    services.RemoveAll<ISmtpDeliveryClient>();
                    services.AddSingleton<ISmtpDeliveryClient>(FakeSmtp);

                    services.PostConfigure<JwtBearerOptions>("ZitadelBearer", options =>
                    {
                        options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
                        {
                            Issuer = "https://test-zitadel.local"
                        };
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = "https://test-zitadel.local",
                            ValidateAudience = true,
                            ValidAudience = "babasketet",
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = _testOidcKey,
                            NameClaimType = "sub"
                        };
                    });
                });
            });

        using var client = Factory.CreateClient();
        await client.GetAsync("/health/ready");
    }

    public string IssueZitadelJwt(
        string userId,
        bool isPremium = false,
        string[]? roles = null,
        string? email = null,
        string? displayName = null)
    {
        var creds = new SigningCredentials(_testOidcKey, SecurityAlgorithms.RsaSha256);
        var claims = new List<Claim>
        {
            new("sub", userId),
            new("email", email ?? $"{userId}@example.com")
        };
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            claims.Add(new("name", displayName));
        }
        if (isPremium)
        {
            claims.Add(new("plan", "premium"));
        }
        if (roles is { Length: > 0 })
        {
            var dict = roles.ToDictionary(r => r, _ => new Dictionary<string, string> { ["org"] = "babasketet" });
            claims.Add(new("urn:zitadel:iam:org:project:roles", System.Text.Json.JsonSerializer.Serialize(dict)));
        }

        var token = new JwtSecurityToken(
            issuer: "https://test-zitadel.local",
            audience: "babasketet",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public HttpClient CreateClientWithToken(string? bearerToken = null)
    {
        var client = Factory.CreateClient();
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        return client;
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BabasketetDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE purchase_items, purchases, product_price_history,
                           basket_items, shopping_list_items, products,
                           house_invitations, house_members, houses, app_users,
                           system_email_configs, admin_notifications RESTART IDENTITY CASCADE;
            DELETE FROM stores WHERE is_system = FALSE;
            DELETE FROM product_types WHERE is_system = FALSE;
            """);
        FakeSmtp.SentMessages.Clear();
        await DatabaseInitializer.SeedCatalogsAsync(db);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }
}

public sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _utcNow = start;
    public override DateTimeOffset GetUtcNow() => _utcNow;
    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}

public sealed record CapturedSmtpMessage(
    OutboundEmailSettings Settings,
    string ToEmail,
    string Subject,
    string HtmlBody,
    string? TextBody);

public sealed class FakeSmtpDeliveryClient : ISmtpDeliveryClient
{
    public List<CapturedSmtpMessage> SentMessages { get; } = [];

    public Task SendAsync(
        OutboundEmailSettings settings,
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody,
        CancellationToken ct = default)
    {
        SentMessages.Add(new CapturedSmtpMessage(settings, toEmail, subject, htmlBody, textBody));
        return Task.CompletedTask;
    }
}
