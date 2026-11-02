using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Helpers;
using APIHandler.Mappers;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Handlers.Dishes;

public class GetAllDishesHandler : IApiRequestHandler
{
    private static readonly string DishesTableName = ResourceNames.DishesTableName;

    private readonly IDynamoDBContext _dbContext;
    private readonly IReservationRepository _reservations;
    private readonly ITableRepository _tables;

    public GetAllDishesHandler(IDynamoDBContext dbContext, IReservationRepository reservations, ITableRepository tables)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _reservations = reservations;
        _tables = tables;
    }

    public static readonly DynamoDBOperationConfig _dishesConfig = new()
    {
        OverrideTableName = DishesTableName,
    };

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        string? dishType = null;
        string? sortBy = null;
        string? name = null;

        if (request.QueryStringParameters != null)
        {
            request.QueryStringParameters.TryGetValue("dishType", out dishType);
            request.QueryStringParameters.TryGetValue("sortBy", out sortBy);
            request.QueryStringParameters.TryGetValue("name", out name);
        }

        var conditions = new List<ScanCondition>();
        if (request.QueryStringParameters?.TryGetValue("reservationId", out var reservationId) == true)
        {
            if (string.IsNullOrWhiteSpace(reservationId))
                return ResponseBuilder.CreateErrorResponse(400, "Reservation ID is required.", context.Logger);

            var reservation = await _reservations.GetReservationByIdAsync(reservationId);
            if (reservation is null)
                return ResponseBuilder.CreateErrorResponse(404, "Reservation was not found.", context.Logger);
            var table = await _tables.GetTableAsync(reservation.TableId);
            if (table is null || string.IsNullOrWhiteSpace(table.LocationId))
                return ResponseBuilder.CreateErrorResponse(404, "Reservation location was not found.", context.Logger);

            conditions.Add(new ScanCondition(nameof(Dish.LocationId), Amazon.DynamoDBv2.DocumentModel.ScanOperator.Equal, table.LocationId));
        }

        var dishes = await _dbContext
            .ScanAsync<Dish>(conditions, _dishesConfig)
            .GetRemainingAsync();

        var orders = new List<global::APIHandler.Entities.Order>();
        var popularityMap = new Dictionary<string, int>();

        if (sortBy == DishSortingLabels.PopularityAscending ||
            sortBy == DishSortingLabels.PopularityDescending)
        {
            orders = await _dbContext
                .ScanAsync<global::APIHandler.Entities.Order>(new List<ScanCondition>(), new DynamoDBOperationConfig
                {
                    OverrideTableName = ResourceNames.OrdersTableName
                })
                .GetRemainingAsync();

            popularityMap = orders
                .SelectMany(o => o.Dishes)
                .GroupBy(item => item.DishId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        if (!string.IsNullOrEmpty(dishType) &&
            Enum.TryParse<DishType>(dishType, ignoreCase: true, out var parsedType))
        {
            dishes = dishes.Where(d => d.DishType == parsedType).ToList();
        }

        if (!string.IsNullOrEmpty(name))
        {
            dishes = dishes.Where(d => d.Name.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        dishes = SortDishes(dishes, sortBy, popularityMap);

        var response = dishes.ConvertAll(DishMapper.ToDishListResponse);
        return ResponseBuilder.CreateSuccessResponse(response);
    }


    private static List<Dish> SortDishes(List<Dish> dishes, string? sortBy, Dictionary<string, int> popularityMap)
    {
        return sortBy switch
        {
            DishSortingLabels.PriceAscending => dishes.OrderBy(d => d.Price).ToList(),
            DishSortingLabels.PriceDescending => dishes.OrderByDescending(d => d.Price).ToList(),

            DishSortingLabels.PopularityAscending => dishes
                .OrderBy(d => popularityMap.ContainsKey(d.Id) ? popularityMap[d.Id] : 0)
                .ToList(),

            DishSortingLabels.PopularityDescending => dishes
                .OrderByDescending(d => popularityMap.ContainsKey(d.Id) ? popularityMap[d.Id] : 0)
                .ToList(),

            _ => dishes
        };
    }

}
