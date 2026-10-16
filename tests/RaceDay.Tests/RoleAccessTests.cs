using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Authorization;
using RaceDay.Api.Models;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>
/// Verifies the [SessionAuthorize] filter: unauthenticated requests are rejected with 401,
/// and each role is rejected with 403 when reaching for the other role's functionality.
/// </summary>
public class RoleAccessTests
{
    [Fact]
    public void AnonymousRequest_ToProtectedEndpoint_Returns401()
    {
        var attribute = new SessionAuthorizeAttribute();

        var result = TestHelpers.RunAuthFilter(attribute, user: null);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void AnonymousRequest_ToOrganiserEndpoint_Returns401()
    {
        var attribute = new SessionAuthorizeAttribute(Roles.Organiser);

        var result = TestHelpers.RunAuthFilter(attribute, user: null);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void Participant_AccessingOrganiserEndpoint_Returns403()
    {
        var db = TestHelpers.NewDb();
        var participant = TestHelpers.MakeParticipant(db);
        var attribute = new SessionAuthorizeAttribute(Roles.Organiser);

        var result = TestHelpers.RunAuthFilter(attribute, participant);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void Organiser_AccessingParticipantEndpoint_Returns403()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var attribute = new SessionAuthorizeAttribute(Roles.Participant);

        var result = TestHelpers.RunAuthFilter(attribute, organiser);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void Organiser_AccessingOrganiserEndpoint_IsAllowed()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var attribute = new SessionAuthorizeAttribute(Roles.Organiser);

        var result = TestHelpers.RunAuthFilter(attribute, organiser);

        Assert.Null(result);   // null means the filter did not short-circuit
    }

    [Fact]
    public void Participant_AccessingParticipantEndpoint_IsAllowed()
    {
        var db = TestHelpers.NewDb();
        var participant = TestHelpers.MakeParticipant(db);
        var attribute = new SessionAuthorizeAttribute(Roles.Participant);

        var result = TestHelpers.RunAuthFilter(attribute, participant);

        Assert.Null(result);
    }

    [Fact]
    public void AnyLoggedInUser_AccessingSharedEndpoint_IsAllowed()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var participant = TestHelpers.MakeParticipant(db);
        var attribute = new SessionAuthorizeAttribute();   // no roles = any logged-in user

        Assert.Null(TestHelpers.RunAuthFilter(attribute, organiser));
        Assert.Null(TestHelpers.RunAuthFilter(attribute, participant));
    }
}
