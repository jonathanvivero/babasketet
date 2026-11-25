using Babasketet.API.Application;
using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Babasketet.API.UnitTests;

public sealed class DomainCatalogsAndTokensTests
{
    [Fact]
    public void Catalogs_ShouldContainExact24Stores_WithDefaultMarked()
    {
        DomainCatalogs.DefaultStores.Should().HaveCount(24);
        DomainCatalogs.DefaultStores.Count(s => s.IsDefault).Should().Be(1);
        DomainCatalogs.DefaultStores.First(s => s.IsDefault).Name.Should().Be("Default");
        DomainCatalogs.DefaultStores.Select(s => s.Name).Should().Contain([
            "Mercadona", "Carrefour", "Dia", "Lidl", "Aldi", "Tienda de Barrio",
            "Spar", "Condis", "El Jamón", "Eroski", "Consum", "Caprabo", "Costco",
            "Supercor", "Hipercor", "Alcampo/Auchan", "Supeco", "Intermarché",
            "Continente", "Coviran", "Pingo Doce", "Alsara", "Comarket"
        ]);
    }

    [Fact]
    public void Catalogs_ShouldContainExact18ProductTypes_WithExpectedColors()
    {
        DomainCatalogs.DefaultProductTypes.Should().HaveCount(18);
        DomainCatalogs.DefaultProductTypes.Should().Contain(t =>
            t.Name == "Lacteos" && t.FillHex == "#FFFFFF" && t.BorderHex == "#FF00FF");
        DomainCatalogs.DefaultProductTypes.Should().Contain(t =>
            t.Name == "Fruta/Verdura" && t.FillHex == "#86EFAC" && t.BorderHex == null);
        DomainCatalogs.DefaultProductTypes.Should().Contain(t =>
            t.Name == "Mascotas" && t.FillHex == "#F5F5DC" && t.BorderHex == "#000000");
    }

    [Fact]
    public void Catalogs_ShouldContainExact11Formats()
    {
        DomainCatalogs.AllowedFormats.Should().Equal(
            "Ud", "Docena", "Kg", "g", "L", "Cl",
            "Pack-2", "Pack-3", "Pack-4", "Pack-6", "Pack-8");
    }

    [Theory]
    [InlineData("  Leche   Entera ", "leche entera")]
    [InlineData("TOMATES", "tomates")]
    [InlineData("Pan\tde\nMolde", "pan de molde")]
    public void NormalizeProductName_ShouldTrimCollapseWhitespaceAndLowercase(string input, string expected)
    {
        DomainCatalogs.NormalizeProductName(input).Should().Be(expected);
    }

    [Fact]
    public void Invitation_ShouldBeActiveBefore1Hour_AndExpiredAfter1Hour()
    {
        var created = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
        var invitation = new HouseInvitation
        {
            Id = Guid.NewGuid(),
            HouseId = Guid.NewGuid(),
            TokenHash = "abc",
            CreatedAt = created,
            ExpiresAt = created.Add(DomainCatalogs.InvitationLifetime)
        };

        invitation.IsActiveAt(created.AddMinutes(59).AddSeconds(59)).Should().BeTrue();
        invitation.IsActiveAt(created.AddHours(1)).Should().BeFalse();
        invitation.IsActiveAt(created.AddHours(1).AddSeconds(1)).Should().BeFalse();
    }

    [Theory]
    [InlineData("#86EFAC", true)]
    [InlineData("#000000", true)]
    [InlineData("#ffffff", true)]
    [InlineData("86EFAC", false)]
    [InlineData("#FFF", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData(null, false)]
    public void IsValidHexColor_ShouldValidate6DigitHex(string? color, bool expected)
    {
        HouseService.IsValidHexColor(color).Should().Be(expected);
    }

    [Fact]
    public void DeviceTokenService_ShouldIssueAndValidateDeviceId()
    {
        var svc = new DeviceTokenService(new DeviceTokenOptions
        {
            DeviceTokenSigningKey = "unit-test-secret-signing-key-at-least-32-chars"
        });
        var deviceId = Guid.NewGuid();

        var token = svc.IssueToken(deviceId);
        var parsed = svc.TryValidateToken(token);

        parsed.Should().Be(deviceId);
        svc.TryValidateToken("invalid.jwt.token").Should().BeNull();
    }
}
