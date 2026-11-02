using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using AutoFixture.Xunit2;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupReservationServiceTests
{
    private readonly Mock<IGroupReservationRepository> _groupReservationRepositoryMock = new();
    private readonly Mock<ITableRepository> _tableRepositoryMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IGuestSessionRepository> _guestSessionRepositoryMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();

    private readonly GroupReservationService _service;

    public GroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groupReservationRepositoryMock.Object,
            _tableRepositoryMock.Object,
            _reservationRepositoryMock.Object,
            _guestSessionRepositoryMock.Object,
            _orderRepositoryMock.Object,
            _userServiceMock.Object,
            _locationServiceMock.Object);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_ValidInput_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Table table,
        Reservation reservation,
        Location location,
        string expectedUserName)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.TableId = table.Id;
        table.LocationId = location.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountJoinedGuestsAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(0);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        _locationServiceMock
            .Setup(s => s.GetLocationByIdAsync(table.LocationId))
            .ReturnsAsync(location);

        _userServiceMock
            .Setup(s => s.GetUserName(reservation.CustomerId!))
            .ReturnsAsync(expectedUserName);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
        Assert.Equal(table.LocationId, result.LocationId);
        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        Assert.Equal(reservation.StartTime, result.StartTime);
        Assert.Equal(reservation.EndTime, result.EndTime);
        Assert.Equal(table.TableNumber, result.TableNumber);
        Assert.Equal("Green & Tasty", result.RestaurantName);
        Assert.Equal(location.Address, result.LocationAddress);
        Assert.Equal(expectedUserName, result.InviterName);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_InvalidToken_ThrowsNotFound(string token)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(token))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupReservationByTokenAsync(token));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed)]
    [InlineAutoData(GroupReservationStatus.Submitted)]
    public async Task GetGroupReservationByTokenAsync_GroupReservationIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        GroupReservation groupReservation)
    {
        groupReservation.Status = status;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_WhenGuestCountIsMax_ThrowsGuestLimitExceeded(GroupReservation groupReservation, Reservation reservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = reservation.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountJoinedGuestsAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(reservation.GuestCount);

        await Assert.ThrowsAsync<GuestLimitExceededException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_ValidInput_UpdatesGroupReservationStatus(GroupReservation groupReservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(groupReservation);

        GroupReservation? savedGroupReservation = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(r => savedGroupReservation = r);

        await _service.CloseGroupReservationAsync(groupReservation.ReservationId);

        Assert.NotNull(savedGroupReservation);
        Assert.Equal(GroupReservationStatus.Closed, savedGroupReservation.Status);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_GroupReservationNotFound_DoesNotThrowException(string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CloseGroupReservationAsync(reservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_ValidInput_ReturnsToken(Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        GetGroupInviteTokenResponse result = await _service.GetGroupReservationInviteTokenAsync(reservation.Id);

        Assert.Equal(groupReservation.ReservationToken, result.Token);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_InvalidReservationId_ThrowsNotFound(string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetGroupReservationInviteTokenAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_OneGuest_ThrowsValidationException(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Completed)]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    public async Task GetGroupReservationInviteTokenAsync_StatusIsNotReserved_ThrowsValidationException(ReservationStatus reservationStatus, Reservation reservation)
    {
        reservation.Status = reservationStatus;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    public async Task GetGroupReservationInviteTokenAsync_GroupStatusIsNotOpen_ThrowsException(GroupReservationStatus status, Type type, Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = status;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(type, () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_ReturnsExistingOwnerSession()
    {
        var (group, reservation) = SetupOwnedGroup();
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([Session(group.GroupReservationId, "guest", false, false),
                Session(group.GroupReservationId, "owner", true, false)]);

        var context = await _service.GetOwnerSessionIdAsync(reservation.Id, "owner-1");

        Assert.Equal("owner", context.GuestSessionId);
        Assert.Equal(group.GroupReservationId, context.GroupReservationId);
        _guestSessionRepositoryMock.Verify(r => r.SaveOwnerIfAbsentAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_CreatesMissingOwnerSessionWithStableId()
    {
        var (group, reservation) = SetupOwnedGroup();
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([]);

        var context = await _service.GetOwnerSessionIdAsync(reservation.Id, "owner-1");

        Assert.Equal($"owner-session-{group.GroupReservationId}", context.GuestSessionId);
        Assert.Equal(group.GroupReservationId, context.GroupReservationId);
        _guestSessionRepositoryMock.Verify(r => r.SaveOwnerIfAbsentAsync(It.Is<GuestSession>(session =>
            session.Id == context.GuestSessionId && session.IsOwnerSession && session.OwnerUserId == "owner-1" &&
            session.GroupReservationId == group.GroupReservationId)), Times.Once);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_SynchronizesSubmittedOwnerOrderWhileGroupIsOpen()
    {
        var (group, reservation) = SetupOwnedGroup();
        var ownerSession = Session(group.GroupReservationId, "owner-session", true, false);
        ownerSession.Dishes = [];
        var order = Order(reservation.Id);
        order.Status = OrderStatus.SubmittedByCustomer;
        order.Dishes =
        [
            new OrderItem { DishId = "dish-a", Quantity = 2, State = OrderItemState.Pending }
        ];
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([ownerSession]);
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync(order);
        _userServiceMock.Setup(service => service.GetUserName("owner-1")).ReturnsAsync("Olena");

        var context = await _service.GetOwnerSessionIdAsync(reservation.Id, "owner-1");

        Assert.Equal(ownerSession.Id, context.GuestSessionId);
        Assert.Equal("Olena", ownerSession.GuestName);
        Assert.True(ownerSession.IsSubmitted);
        var item = Assert.Single(ownerSession.Dishes);
        Assert.Equal("dish-a", item.DishId);
        Assert.Equal(2, item.Quantity);
        _guestSessionRepositoryMock.Verify(r => r.SaveAsync(ownerSession), Times.Once);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_DoesNotOverwriteOwnerSessionAfterGroupSubmit()
    {
        var (group, reservation) = SetupOwnedGroup();
        group.Status = GroupReservationStatus.Submitted;
        var ownerSession = Session(group.GroupReservationId, "owner-session", true, true);
        ownerSession.Dishes = [new OrderItem { DishId = "owner-dish", Quantity = 1 }];
        var finalOrder = Order(reservation.Id);
        finalOrder.Status = OrderStatus.SubmittedByCustomer;
        finalOrder.Dishes = [new OrderItem { DishId = "consolidated-dish", Quantity = 5 }];
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([ownerSession]);
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync(finalOrder);

        await _service.GetOwnerSessionIdAsync(reservation.Id, "owner-1");

        Assert.Equal("owner-dish", Assert.Single(ownerSession.Dishes).DishId);
        _guestSessionRepositoryMock.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
        _orderRepositoryMock.Verify(r => r.GetOrderByReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_RejectsOtherCustomerBeforeLoadingSessions()
    {
        var (_, reservation) = SetupOwnedGroup();

        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _service.GetOwnerSessionIdAsync(reservation.Id, "other"));

        _guestSessionRepositoryMock.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_MissingGroupReturnsNotFound()
    {
        var (_, reservation) = SetupOwnedGroup();
        _groupReservationRepositoryMock.Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _service.GetOwnerSessionIdAsync(reservation.Id, "owner-1"));
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_MissingReservationReturnsNotFound()
    {
        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _service.GetOwnerSessionIdAsync("missing-reservation", "owner-1"));
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_MergesOnlySubmittedSessionsAndUpdatesOrderAndGroup()
    {
        var (group, reservation) = SetupOwnedGroup();
        var first = Session(group.GroupReservationId, "first", false, true);
        first.Dishes = [new OrderItem { DishId = "dish-a", Quantity = 2 },
            new OrderItem { DishId = "dish-b", Quantity = 1 }];
        var second = Session(group.GroupReservationId, "second", true, true);
        second.Dishes = [new OrderItem { DishId = "dish-a", Quantity = 1 }];
        var draft = Session(group.GroupReservationId, "draft", false, false);
        draft.Dishes = [new OrderItem { DishId = "dish-c", Quantity = 9 }];
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([first, second, draft]);
        var order = Order(reservation.Id);
        order.Dishes = [new OrderItem { DishId = "old-dish", Quantity = 5 }];
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync(order);

        var result = await _service.SubmitGroupPreOrderAsync(group.GroupReservationId, reservation.Id, "owner-1");

        Assert.Collection(result,
            item => { Assert.Equal("dish-a", item.DishId); Assert.Equal(3, item.Quantity); },
            item => { Assert.Equal("dish-b", item.DishId); Assert.Equal(1, item.Quantity); });
        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(GroupReservationStatus.Submitted, group.Status);
        Assert.Equal(2, order.Dishes.Count);
        Assert.All(order.Dishes, item => Assert.Equal(OrderItemState.Pending, item.State));
        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(order), Times.Once);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(group), Times.Once);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_NoSubmittedSessionsDoesNotWrite()
    {
        var (group, reservation) = SetupOwnedGroup();
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([Session(group.GroupReservationId, "draft", false, false)]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupPreOrderAsync(group.GroupReservationId, reservation.Id, "owner-1"));

        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_EmptySubmittedCartDoesNotWrite()
    {
        var (group, reservation) = SetupOwnedGroup();
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([Session(group.GroupReservationId, "empty", false, true)]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupPreOrderAsync(group.GroupReservationId, reservation.Id, "owner-1"));

        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_MissingOrderDoesNotSubmitGroup()
    {
        var (group, reservation) = SetupOwnedGroup();
        var submitted = Session(group.GroupReservationId, "guest", false, true);
        submitted.Dishes = [new OrderItem { DishId = "dish-a", Quantity = 1 }];
        _guestSessionRepositoryMock.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([submitted]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupPreOrderAsync(group.GroupReservationId, reservation.Id, "owner-1"));

        Assert.Equal(GroupReservationStatus.Open, group.Status);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_WrongReservationDoesNotWrite()
    {
        var (group, _) = SetupOwnedGroup();
        _reservationRepositoryMock.Setup(r => r.GetReservationByIdAsync("other-reservation"))
            .ReturnsAsync(new Reservation { Id = "other-reservation", TableId = "table-1", WaiterId = "waiter-1",
                CustomerId = "owner-1", Status = ReservationStatus.Reserved });

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupPreOrderAsync(group.GroupReservationId, "other-reservation", "owner-1"));

        _guestSessionRepositoryMock.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_RejectsOtherCustomerBeforeLoadingSessions()
    {
        var (group, reservation) = SetupOwnedGroup();

        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _service.SubmitGroupPreOrderAsync(group.GroupReservationId, reservation.Id, "other"));

        _guestSessionRepositoryMock.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_MissingGroupOrReservationReturnsNotFound()
    {
        var (group, _) = SetupOwnedGroup();

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _service.SubmitGroupPreOrderAsync("missing-group", "reservation-1", "owner-1"));
        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _service.SubmitGroupPreOrderAsync(group.GroupReservationId, "missing-reservation", "owner-1"));
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_AlreadySubmittedDoesNotWrite()
    {
        var (group, reservation) = SetupOwnedGroup();
        group.Status = GroupReservationStatus.Submitted;

        await Assert.ThrowsAsync<GroupReservationSubmittedException>(() =>
            _service.SubmitGroupPreOrderAsync(group.GroupReservationId, reservation.Id, "owner-1"));

        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(false, 0)]
    [InlineData(true, 3)]
    [InlineData(true, 0)]
    public async Task UpdateParticipantDishAsync_UpdatesOwnerAndGuestCarts(bool owner, int quantity)
    {
        var (group, reservation) = SetupOwnedGroup();
        var session = Session(group.GroupReservationId, "session-1", owner, true);
        session.Dishes = [new OrderItem { DishId = "dish-a", Quantity = 1 }];
        _guestSessionRepositoryMock.Setup(r => r.GetByGuestSessionId(session.Id)).ReturnsAsync(session);
        var order = Order(reservation.Id);
        order.Status = OrderStatus.SubmittedByCustomer;
        order.Dishes = [new OrderItem { DishId = "dish-a", Quantity = 1 }];
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync(order);

        var result = await _service.UpdateParticipantDishAsync("owner-1", group.GroupReservationId, session.Id, "dish-a", quantity);

        Assert.Equal(quantity, result.Dishes.Sum(item => item.Quantity));
        Assert.Equal(quantity > 0, result.IsSubmitted);
        _guestSessionRepositoryMock.Verify(r => r.SaveAsync(session), Times.Once);
        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), owner ? Times.Once() : Times.Never());
        if (owner) Assert.Equal(quantity, order.Dishes.Sum(item => item.Quantity));
    }

    [Fact]
    public async Task UpdateParticipantDishAsync_RejectsOtherCustomerAndWrongGroup()
    {
        var (group, _) = SetupOwnedGroup();
        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _service.UpdateParticipantDishAsync("other", group.GroupReservationId, "session", "dish-a", 2));
        var session = Session("other-group", "session", false, true);
        _guestSessionRepositoryMock.Setup(r => r.GetByGuestSessionId("session")).ReturnsAsync(session);
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateParticipantDishAsync("owner-1", group.GroupReservationId, "session", "dish-a", 2));
        _guestSessionRepositoryMock.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [InlineData(GroupReservationStatus.Submitted)]
    [InlineData(GroupReservationStatus.Closed)]
    public async Task UpdateParticipantDishAsync_RejectsFinalizedGroups(GroupReservationStatus status)
    {
        var (group, _) = SetupOwnedGroup();
        group.Status = status;
        await Assert.ThrowsAsync<GroupReservationExpiredException>(() =>
            _service.UpdateParticipantDishAsync("owner-1", group.GroupReservationId, "session", "dish-a", 2));
        _guestSessionRepositoryMock.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task UpdateParticipantDishAsync_RejectsNegativeQuantity()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateParticipantDishAsync("owner-1", "group-1", "session", "dish-a", -1));
    }

    private (GroupReservation Group, Reservation Reservation) SetupOwnedGroup()
    {
        var group = new GroupReservation
        {
            GroupReservationId = "group-1", ReservationId = "reservation-1", ReservationToken = "token",
            Status = GroupReservationStatus.Open
        };
        var reservation = new Reservation
        {
            Id = group.ReservationId, TableId = "table-1", WaiterId = "waiter-1",
            CustomerId = "owner-1", Status = ReservationStatus.Reserved, GuestCount = 3
        };
        _groupReservationRepositoryMock.Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _groupReservationRepositoryMock.Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(group);
        _reservationRepositoryMock.Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        return (group, reservation);
    }

    private static GuestSession Session(string groupId, string id, bool owner, bool submitted) => new()
    {
        Id = id, GroupReservationId = groupId, GuestName = id, Dishes = [],
        IsOwnerSession = owner, IsSubmitted = submitted
    };

    private static Order Order(string reservationId) => new()
    {
        OrderId = "order-1", CustomerId = "owner-1", ReservationId = reservationId,
        ReservationAddress = "Address", ReservationTime = "Tomorrow", Dishes = []
    };
}
