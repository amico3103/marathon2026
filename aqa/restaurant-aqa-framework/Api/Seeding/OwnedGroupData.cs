using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Dtos.Users;
using Restaurant_Aqa_Framework.Configuration;
using System.Security.Cryptography;
using System.Text.Json;

namespace Restaurant_Aqa_Framework.Api.Seeding;

public enum GroupDataShape { Review, MixedReview, EmptyGroup, OwnerOnly, CustomerAndTable, AllConfirmed, PendingOnly, NoOrder, SubmittedEmpty, OwnerPreOrder }

public sealed class GroupGuest(string sessionId, string token, string name, DishListResponse dish, int quantity, bool submitted)
{
    public string SessionId { get; } = sessionId;
    public string Token { get; } = token;
    public string Name { get; } = name;
    public DishListResponse Dish { get; } = dish;
    public int Quantity { get; } = quantity;
    public bool Submitted { get; } = submitted;
}

public sealed class OwnedGroupData : IAsyncDisposable
{
    private readonly IAPIRequestContext _api;

    public OwnedGroupCleanup Cleanup { get; } = new();
    public string OwnerEmail => Cleanup.Username;
    public string OwnerPassword { get; } = "Aqa!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(5)) + "Aa";
    public string OwnerToken { get; private set; } = "";
    public string WaiterToken { get; private set; } = "";
    public string GroupId { get; private set; } = "";
    public string OwnerSessionId { get; private set; } = "";
    public ReservationResponse Reservation { get; private set; } = null!;
    public DishListResponse Dish { get; private set; } = null!;
    public TableResponse Table { get; private set; } = null!;
    public string LocationAddress { get; private set; } = "";
    public string LocationId { get; private set; } = "";
    public string InviteToken { get; private set; } = "";
    public DateTime BookingStart { get; } = DateTime.UtcNow.Date.AddDays(2).AddHours(17);
    public List<GroupGuest> Guests { get; } = [];

    private OwnedGroupData(IAPIRequestContext api) => _api = api;

    public static async Task<OwnedGroupData> CreateAsync(IAPIRequestContext api, GroupDataShape shape = GroupDataShape.Review)
    {
        var data = new OwnedGroupData(api);
        try
        {
            await data.PrepareAsync(shape);
            return data;
        }
        catch (Exception setupError)
        {
            try { await data.DisposeAsync(); }
            catch (Exception cleanupError) { throw new AggregateException("Fixture preparation and cleanup failed.", setupError, cleanupError); }
            throw;
        }
    }

    public async Task<JsonElement> GetSnapshotAsync() =>
        await ReadAsync<JsonElement>(await new GroupOrdersApi(_api).GetSessionsAsync(GroupId, OwnerToken));

    public ValueTask DisposeAsync() => Cleanup.DisposeAsync();

    public async Task AdoptReservationAsync(ReservationResponse reservation)
    {
        if (reservation.CustomerId != Cleanup.OwnerId || reservation.TableId != Table.Id)
            throw new InvalidOperationException("The reservation is not owned by this fixture.");
        Reservation = reservation;
        var groups = new GroupOrdersApi(_api);
        var invitation = await ReadAsync<JsonElement>(await groups.GetInviteTokenAsync(reservation.Id, OwnerToken));
        InviteToken = invitation.GetProperty("token").GetString()!;
        var details = await ReadAsync<JsonElement>(await groups.GetInviteAsync(InviteToken));
        GroupId = details.GetProperty("GroupReservationId").GetString()!;
        var dishes = await ReadAsync<List<DishListResponse>>(await new DishesApi(_api).GetDishesAsync(reservationId: reservation.Id));
        Dish = dishes.First(dish => dish.State == "Available");
    }

    private async Task PrepareAsync(GroupDataShape shape)
    {
        var settings = Settings.Current;
        if (string.IsNullOrWhiteSpace(settings.TestTableId))
            throw new InvalidOperationException("Set TESTTABLE_ID before preparing group test data.");
        await Cleanup.CreateCustomerAsync(OwnerPassword);
        var auth = new AuthApi(_api);
        OwnerToken = (await ReadAsync<SignInResponse>(await auth.SignInAsync(new(OwnerEmail, OwnerPassword)))).IdToken;
        WaiterToken = await auth.SignInAsWaiterAsync();
        var waiterProfile = await ReadAsync<UserDto>(await new UsersApi(_api).GetMeAsync(WaiterToken));
        if (waiterProfile.Role != "Waiter")
            throw new InvalidOperationException("The configured account is not a waiter.");
        var waiterId = waiterProfile.Id;
        var locationId = await Cleanup.GetWaiterLocationAsync(waiterId, settings.TestTableId);
        LocationId = locationId;
        var location = await ReadAsync<JsonElement>(await new LocationsApi(_api).GetLocationAsync(locationId));
        LocationAddress = location.GetProperty("address").GetString()!;
        Table = await ReadAsync<TableResponse>(await new TablesApi(_api).CreateTableAsync(
            new(locationId, 4, RandomNumberGenerator.GetInt32(10000, int.MaxValue)), WaiterToken));
        Cleanup.TrackTable(Table.Id, locationId);
        if (shape == GroupDataShape.CustomerAndTable) return;
        var reservation = await ReadAsync<ReservationResponse>(await new ReservationsApi(_api).CreateReservationAsync(
            new(Table.Id, BookingStart, BookingStart.AddMinutes(90), 3, CustomerEmail: OwnerEmail), WaiterToken));
        await AdoptReservationAsync(reservation);
        if (Reservation.WaiterId != waiterId)
            throw new InvalidOperationException("The reservation owner or assigned waiter does not match the fixture.");
        if (shape == GroupDataShape.EmptyGroup) return;
        var groups = new GroupOrdersApi(_api);
        var owner = await ReadAsync<JsonElement>(await groups.GetOwnerSessionAsync(Reservation.Id, OwnerToken));
        GroupId = owner.GetProperty("groupReservationId").GetString()!;
        OwnerSessionId = owner.GetProperty("guestSessionId").GetString()!;
        if (shape == GroupDataShape.OwnerOnly) return;
        if (shape is GroupDataShape.PendingOnly or GroupDataShape.NoOrder or GroupDataShape.SubmittedEmpty)
        {
            await AddGuestAsync("AQA-Sample-Alex", 1, submitted: shape != GroupDataShape.PendingOnly);
            if (shape == GroupDataShape.SubmittedEmpty)
                await Cleanup.PrepareEmptySubmittedCartAsync(GroupId, Guests[0].SessionId);
            return;
        }
        var orders = new OrdersApi(_api);
        await RequireSuccessAsync(await orders.CreateOrderAsync(new(Reservation.Id, [new(Dish.Id, 1)]), OwnerToken));
        await RequireSuccessAsync(await orders.SubmitOrderAsync(Reservation.Id, OwnerToken));
        await RequireSuccessAsync(await groups.GetOwnerSessionAsync(Reservation.Id, OwnerToken));
        if (shape == GroupDataShape.OwnerPreOrder) return;
        var dishes = await ReadAsync<List<DishListResponse>>(await new DishesApi(_api).GetDishesAsync(reservationId: Reservation.Id));
        var available = dishes.Where(dish => dish.State == "Available").ToArray();
        if (shape == GroupDataShape.MixedReview && available.Length < 2)
            throw new InvalidOperationException("The mixed-cart scenario requires at least two available dishes.");
        await AddGuestAsync("AQA-Sample-Alex", 2, submitted: true,
            shape == GroupDataShape.MixedReview ? available.First(dish => dish.Id != Dish.Id) : Dish);
        await AddGuestAsync("AQA-Sample-Maria", 1, submitted: shape == GroupDataShape.AllConfirmed,
            shape == GroupDataShape.MixedReview ? available[^1] : Dish);
    }

    public async Task AddGuestAsync(string name, int quantity, bool submitted, DishListResponse? dish = null)
    {
        var groups = new GroupOrdersApi(_api);
        var guest = await ReadAsync<JsonElement>(await groups.CreateGuestAsync(GroupId, name));
        var sessionId = guest.GetProperty("SessionId").GetString()!;
        var token = guest.GetProperty("SessionToken").GetString()!;
        dish ??= Dish;
        if (quantity > 0) await RequireSuccessAsync(await groups.AddDishAsync(sessionId, token, dish.Id, quantity));
        if (submitted) await RequireSuccessAsync(await groups.SubmitGuestAsync(sessionId, token));
        Guests.Add(new(sessionId, token, name, dish, quantity, submitted));
    }

    private static async Task<T> ReadAsync<T>(IAPIResponse response)
    {
        await RequireSuccessAsync(response);
        return await response.ReadAsAsync<T>();
    }

    private static async Task RequireSuccessAsync(IAPIResponse response)
    {
        if (!response.Ok)
            throw new InvalidOperationException($"Fixture request failed: HTTP {response.Status}: {await response.ReadErrorMessageAsync()}");
    }
}
