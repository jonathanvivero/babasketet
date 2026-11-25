using Babasketet.API.Application;
using Babasketet.API.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Babasketet.API.UnitTests;

public sealed class ShoppingRulesTests
{
    [Fact]
    public void DomainExceptions_ShouldMapToExpectedHttpStatusCodes()
    {
        new HouseLimitExceededException(1, true).StatusCode.Should().Be(StatusCodes.Status409Conflict);
        new InvitationExpiredException().StatusCode.Should().Be(StatusCodes.Status410Gone);
        new PremiumRequiredException().StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        new ForbiddenAccessException().StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        new DomainValidationException("ERR", "msg").StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void CallerIdentity_ShouldDistinguishRegisteredVsAnonymous()
    {
        var anon = new CallerIdentity(null, Guid.NewGuid(), false);
        anon.IsAnonymous.Should().BeTrue();
        anon.IsRegistered.Should().BeFalse();

        var reg = new CallerIdentity("user-123", null, true);
        reg.IsRegistered.Should().BeTrue();
        reg.IsAnonymous.Should().BeFalse();
        reg.IsPremium.Should().BeTrue();
    }
}
