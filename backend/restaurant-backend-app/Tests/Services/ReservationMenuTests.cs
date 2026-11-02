using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Handlers.Dishes;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using Moq;

namespace APIHandler.Tests.Services;

public class ReservationMenuTests
{
    [Fact]
    public async Task FiltersDishesUsingTheReservationsTableLocation()
    {
        Environment.SetEnvironmentVariable("dishesTable", "Dishes");
        var db = new Mock<IDynamoDBContext>();
        var reservations = new Mock<IReservationRepository>();
        var tables = new Mock<ITableRepository>();
        reservations.Setup(r => r.GetReservationByIdAsync("reservation-1")).ReturnsAsync(new Reservation
        {
            Id = "reservation-1", TableId = "table-1", WaiterId = "waiter-1"
        });
        tables.Setup(t => t.GetTableAsync("table-1")).ReturnsAsync(new APIHandler.Entities.Table
        {
            Id = "table-1", LocationId = "location-1"
        });
        var search = new Mock<AsyncSearch<Dish>>();
        search.Setup(s => s.GetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        db.Setup(d => d.ScanAsync<Dish>(It.IsAny<IEnumerable<ScanCondition>>(), It.IsAny<DynamoDBOperationConfig>()))
            .Returns(search.Object);
        var context = new Mock<ILambdaContext>();
        context.SetupGet(c => c.Logger).Returns(Mock.Of<ILambdaLogger>());

        var response = await new GetAllDishesHandler(db.Object, reservations.Object, tables.Object)
            .HandleRequest(new APIGatewayProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> { ["reservationId"] = "reservation-1" }
            }, context.Object);

        Assert.Equal(200, response.StatusCode);
        db.Verify(d => d.ScanAsync<Dish>(
            It.Is<IEnumerable<ScanCondition>>(conditions => conditions.Any(c =>
                c.PropertyName == nameof(Dish.LocationId) && c.Operator == ScanOperator.Equal &&
                (string)c.Values[0] == "location-1")), It.IsAny<DynamoDBOperationConfig>()), Times.Once);
    }
}
