using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Services;

public class GetGroupSessionsHandlerTests
{
    private readonly Mock<IGuestSessionService> _service = new();
    private readonly Mock<ILambdaContext> _context = new();

    public GetGroupSessionsHandlerTests()
    {
        _context.SetupGet(context => context.Logger).Returns(Mock.Of<ILambdaLogger>());
    }

    [Fact]
    public async Task HandleRequest_Owner_ReturnsCamelCaseSnapshot()
    {
        var snapshot = new GetAllSessionsResponse(
            [new Participant("session-1", ParticipantType.Guest, "Olena",
                [new ParticipantItem(new ParticipantDish("dish-2", "Tom Yum", 220), 1)], 220, false)], 220);
        _service.Setup(service => service.GetGroupSessionsAsync("owner-1", "group-1")).ReturnsAsync(snapshot);

        var response = await new GetGroupSessionsHandler(_service.Object)
            .HandleRequest(CreateRequest("owner-1"), _context.Object);

        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal(220, body.RootElement.GetProperty("total").GetDecimal());
        var participant = Assert.Single(body.RootElement.GetProperty("participants").EnumerateArray());
        Assert.Equal("session-1", participant.GetProperty("id").GetString());
        Assert.Equal("Guest", participant.GetProperty("type").GetString());
        Assert.False(participant.GetProperty("isSubmitted").GetBoolean());
        var item = Assert.Single(participant.GetProperty("items").EnumerateArray());
        Assert.Equal(1, item.GetProperty("quantity").GetInt32());
        var dish = item.GetProperty("dish");
        Assert.Equal("dish-2", dish.GetProperty("id").GetString());
        Assert.Equal("Tom Yum", dish.GetProperty("name").GetString());
        Assert.Equal(220, dish.GetProperty("price").GetDecimal());
        Assert.Equal(3, dish.EnumerateObject().Count());
    }

    [Fact]
    public async Task HandleRequest_WithoutIdentity_ReturnsUnauthorized()
    {
        var response = await new GetGroupSessionsHandler(_service.Object)
            .HandleRequest(CreateRequest(null), _context.Object);

        Assert.Equal(401, response.StatusCode);
        _service.Verify(service => service.GetGroupSessionsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_OtherCustomer_ReturnsForbidden()
    {
        _service.Setup(service => service.GetGroupSessionsAsync("other-customer", "group-1"))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        var response = await new GetGroupSessionsHandler(_service.Object)
            .HandleRequest(CreateRequest("other-customer"), _context.Object);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_UnknownGroup_ReturnsNotFound()
    {
        _service.Setup(service => service.GetGroupSessionsAsync("owner-1", "group-1"))
            .ThrowsAsync(new GroupReservationNotFoundException());

        var response = await new GetGroupSessionsHandler(_service.Object)
            .HandleRequest(CreateRequest("owner-1"), _context.Object);

        Assert.Equal(404, response.StatusCode);
    }

    private static APIGatewayProxyRequest CreateRequest(string? userId)
    {
        return new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { ["groupReservationId"] = "group-1" },
            RequestContext = userId is null ? null : new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { ["sub"] = userId }
                }
            }
        };
    }
}
