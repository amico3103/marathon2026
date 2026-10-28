using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Services;

public class LevelTwoGroupHandlersTests
{
    private readonly Mock<IGroupReservationService> _service = new();
    private readonly Mock<ILambdaContext> _context = new();

    public LevelTwoGroupHandlersTests()
    {
        _context.SetupGet(context => context.Logger).Returns(Mock.Of<ILambdaLogger>());
    }

    [Fact]
    public async Task GetOwnerSessionId_ReturnsIdForOwner()
    {
        _service.Setup(service => service.GetOwnerSessionIdAsync("reservation-1", "owner-1"))
            .ReturnsAsync("owner-session-1");

        var response = await new GetOwnerSessionIdHandler(_service.Object)
            .HandleRequest(Request("owner-1", "reservationId", "reservation-1"), _context.Object);

        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal("owner-session-1", body.RootElement.GetProperty("guestSessionId").GetString());
    }

    [Fact]
    public async Task GetOwnerSessionId_RequiresAuthentication()
    {
        var response = await new GetOwnerSessionIdHandler(_service.Object)
            .HandleRequest(Request(null, "reservationId", "reservation-1"), _context.Object);

        Assert.Equal(401, response.StatusCode);
        _service.Verify(service => service.GetOwnerSessionIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrder_ReturnsMergedDishQuantities()
    {
        _service.Setup(service => service.SubmitGroupPreOrderAsync("group-1", "reservation-1", "owner-1"))
            .ReturnsAsync([new ConsolidatedOrderItem("dish-a", 3)]);

        var response = await new SubmitGroupPreOrderHandler(_service.Object)
            .HandleRequest(Request("owner-1", "groupReservationId", "group-1", "{\"reservationId\":\"reservation-1\"}"), _context.Object);

        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        var item = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal("dish-a", item.GetProperty("dishId").GetString());
        Assert.Equal(3, item.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task SubmitGroupPreOrder_RejectsMissingReservationId()
    {
        var response = await new SubmitGroupPreOrderHandler(_service.Object)
            .HandleRequest(Request("owner-1", "groupReservationId", "group-1", "{}"), _context.Object);

        Assert.Equal(400, response.StatusCode);
        _service.Verify(service => service.SubmitGroupPreOrderAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrder_MapsForbiddenTo403()
    {
        _service.Setup(service => service.SubmitGroupPreOrderAsync("group-1", "reservation-1", "other"))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        var response = await new SubmitGroupPreOrderHandler(_service.Object)
            .HandleRequest(Request("other", "groupReservationId", "group-1", "{\"reservationId\":\"reservation-1\"}"), _context.Object);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task SubmitGroupPreOrder_MapsMissingGroupTo404()
    {
        _service.Setup(service => service.SubmitGroupPreOrderAsync("group-1", "reservation-1", "owner-1"))
            .ThrowsAsync(new GroupReservationNotFoundException());

        var response = await new SubmitGroupPreOrderHandler(_service.Object)
            .HandleRequest(Request("owner-1", "groupReservationId", "group-1", "{\"reservationId\":\"reservation-1\"}"), _context.Object);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task SubmitGroupPreOrder_MapsAlreadySubmittedTo409()
    {
        _service.Setup(service => service.SubmitGroupPreOrderAsync("group-1", "reservation-1", "owner-1"))
            .ThrowsAsync(new GroupReservationSubmittedException());

        var response = await new SubmitGroupPreOrderHandler(_service.Object)
            .HandleRequest(Request("owner-1", "groupReservationId", "group-1", "{\"reservationId\":\"reservation-1\"}"), _context.Object);

        Assert.Equal(409, response.StatusCode);
    }

    private static APIGatewayProxyRequest Request(string? userId, string pathName, string pathValue, string? body = null) => new()
    {
        PathParameters = new Dictionary<string, string> { [pathName] = pathValue },
        Body = body,
        RequestContext = userId is null ? null : new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = new Dictionary<string, string> { ["sub"] = userId }
            }
        }
    };
}
