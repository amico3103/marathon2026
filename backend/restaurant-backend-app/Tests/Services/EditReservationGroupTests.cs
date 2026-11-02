using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Handlers.Reservations;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Services;

public class EditReservationGroupTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EditingSoloReservationCreatesGroupOnlyWhenRequired(int guests)
    {
        Environment.SetEnvironmentVariable("reservationsTable", "Reservations");
        Environment.SetEnvironmentVariable("tablesTable", "Tables");
        Environment.SetEnvironmentVariable("reservationsTableGlobalIndex", "GSI_ByTableId");
        var db = new Mock<IDynamoDBContext>();
        var locations = new Mock<ILocationService>();
        var groups = new Mock<IGroupReservationService>();
        var reservation = new Reservation { Id = "reservation-1", TableId = "table-1", CustomerId = "owner-1", WaiterId = "waiter-1", GuestCount = 1, Status = ReservationStatus.Reserved };
        db.Setup(d => d.LoadAsync<Reservation>(It.IsAny<object>(), It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        db.Setup(d => d.LoadAsync<APIHandler.Entities.Table>(It.IsAny<object>(), It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new APIHandler.Entities.Table { Id = "table-1", LocationId = "location-1", Capacity = 4 });
        var search = new Mock<AsyncSearch<Reservation>>();
        search.Setup(s => s.GetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        db.Setup(d => d.QueryAsync<Reservation>(It.IsAny<object>(), It.IsAny<QueryConfig>())).Returns(search.Object);
        locations.Setup(l => l.GetLocationByTableIdAsync("table-1")).ReturnsAsync(new Location { Id = "location-1", Address = "Test address", Description = "Test", AverageOccupancy = "Low" });
        var context = new Mock<ILambdaContext>();
        context.SetupGet(c => c.Logger).Returns(Mock.Of<ILambdaLogger>());
        var start = DateTime.UtcNow.AddDays(1);
        var request = new APIGatewayProxyRequest {
            Body = JsonSerializer.Serialize(new { reservationId = "reservation-1", guestCount = guests, startTime = start, endTime = start.AddHours(1) }),
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext { Authorizer = new APIGatewayCustomAuthorizerContext { Claims = new Dictionary<string, string> { ["sub"] = "owner-1", ["cognito:groups"] = "Customer" } } }
        };

        var response = await new EditReservationHandler(db.Object, locations.Object, groups.Object).HandleRequest(request, context.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(guests, reservation.GuestCount);
        groups.Verify(g => g.CreateGroupReservationIfDoesNotExistAsync("owner-1", "reservation-1"), guests >= 2 ? Times.Once() : Times.Never());
    }
}
