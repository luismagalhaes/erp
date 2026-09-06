using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Core.Tests;

/// <summary>
/// Reproduces the claim handling the APIs configure. Duende issues the role as "role"; the JWT
/// handler renames it unless inbound mapping is turned off, and a renamed claim makes every
/// [Authorize(Roles = ...)] endpoint answer 403 to a user who does have the role.
/// </summary>
public class JwtRoleClaimTests
{
    private const string RoleClaimType = "role";
    private const string WsFederationRoleClaimType = ClaimTypes.Role;

    private static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("erp-tests-signing-key-with-enough-length-32+"));

    private static string CreateTokenWithRole(string role)
    {
        var handler = new JsonWebTokenHandler();

        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "12f5e1d5-06f7-4c32-bdca-8254e6a41de2",
                ["name"] = "lmagalhaes@cegid.com",
                [RoleClaimType] = role
            },
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        });
    }

    private static async Task<ClaimsPrincipal> ValidateAsync(string token, bool mapInboundClaims)
    {
        var handler = new JsonWebTokenHandler { MapInboundClaims = mapInboundClaims };

        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            IssuerSigningKey = SigningKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            RoleClaimType = RoleClaimType,
            NameClaimType = "name"
        });

        result.IsValid.Should().BeTrue();
        return new ClaimsPrincipal(result.ClaimsIdentity);
    }

    [Fact]
    public void JwtBearer_maps_inbound_claims_by_default()
    {
        new JwtBearerOptions().MapInboundClaims
            .Should().BeTrue("this default is exactly why the APIs turn it off explicitly");
    }

    [Fact]
    public async Task Without_mapping_the_role_claim_keeps_its_name_and_authorization_works()
    {
        var principal = await ValidateAsync(CreateTokenWithRole("SuperAdmin"), mapInboundClaims: false);

        principal.FindFirst(RoleClaimType).Should().NotBeNull();
        principal.IsInRole("SuperAdmin").Should().BeTrue();
    }

    [Fact]
    public async Task With_mapping_the_role_is_renamed_and_authorization_silently_fails()
    {
        var principal = await ValidateAsync(CreateTokenWithRole("SuperAdmin"), mapInboundClaims: true);

        principal.FindFirst(RoleClaimType).Should().BeNull();
        principal.FindFirst(WsFederationRoleClaimType).Should().NotBeNull();
        principal.IsInRole("SuperAdmin").Should().BeFalse(
            "the claim was renamed while the principal still looks for the short name: this is the 403");
    }

    [Fact]
    public async Task Without_mapping_the_subject_claim_stays_readable_as_sub()
    {
        var principal = await ValidateAsync(CreateTokenWithRole("Admin"), mapInboundClaims: false);

        principal.FindFirst("sub")!.Value.Should().Be("12f5e1d5-06f7-4c32-bdca-8254e6a41de2");
    }
}
