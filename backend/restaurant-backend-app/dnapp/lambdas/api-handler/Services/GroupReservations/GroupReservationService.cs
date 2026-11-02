using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Services.GroupReservations;

public class GroupReservationService(
    IGroupReservationRepository groupReservationRepository,
    ITableRepository tableRepository,
    IReservationRepository reservationRepository,
    IGuestSessionRepository guestSessionRepository,
    IOrderRepository orderRepository,
    IUserService userService,
    ILocationService locationService) : IGroupReservationService
{
    private const string DefaultRestaurantName = "Green & Tasty";

    public async Task<GuestSession> UpdateParticipantDishAsync(
        string userId, string groupId, string sessionId, string dishId, int quantity)
    {
        if (quantity < 0 || string.IsNullOrWhiteSpace(dishId))
            throw new ValidationException("Dish ID is required and quantity cannot be negative.");
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupId)
            ?? throw new GroupReservationNotFoundException();
        var reservation = await GetOwnedReservationAsync(group.ReservationId, userId);
        if (group.Status != GroupReservationStatus.Open || reservation.Status != ReservationStatus.Reserved)
            throw new GroupReservationExpiredException("Only an open group reservation can be edited.");
        var session = await guestSessionRepository.GetByGuestSessionId(sessionId)
            ?? throw new GuestSessionNotFoundException("Guest session was not found.");
        if (session.GroupReservationId != groupId)
            throw new ValidationException("Guest session does not belong to this group reservation.");
        var item = session.Dishes.FirstOrDefault(item => item.DishId == dishId)
            ?? throw new DishNotFoundException("Dish was not found in this cart.");

        // The owner's source of truth is Order; update it too so polling cannot undo the edit.
        if (session.IsOwnerSession)
        {
            var order = await orderRepository.GetOrderByReservationIdAsync(reservation.Id)
                ?? throw new ValidationException("Order was not found for this reservation.");
            if (order.Status == OrderStatus.Cancelled)
                throw new ValidationException("The owner's order is cancelled.");
            var orderItem = order.Dishes.FirstOrDefault(dish => dish.DishId == dishId)
                ?? throw new DishNotFoundException("Dish was not found in the owner's order. Refresh and try again.");
            if (quantity == 0) order.Dishes.Remove(orderItem);
            else orderItem.Quantity = quantity;
            if (order.Dishes.Count == 0) order.Status = OrderStatus.Created;
            await orderRepository.SaveOrderAsync(order);
            session.IsSubmitted = order.Status == OrderStatus.SubmittedByCustomer;
        }
        if (quantity == 0) session.Dishes.Remove(item);
        else item.Quantity = quantity;
        if (session.Dishes.Count == 0) session.IsSubmitted = false;
        await guestSessionRepository.SaveAsync(session);
        return session;
    }

    public async Task<(string GuestSessionId, string GroupReservationId)> GetOwnerSessionIdAsync(
        string reservationId,
        string userId)
    {
        var reservation = await GetOwnedReservationAsync(reservationId, userId);
        var group = await groupReservationRepository.GetByReservationIdAsync(reservation.Id)
            ?? throw new GroupReservationNotFoundException();

        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(group.GroupReservationId);
        var ownerSession = sessions.FirstOrDefault(session => session.IsOwnerSession);
        if (ownerSession is null)
        {
            ownerSession = new GuestSession
            {
                Id = $"owner-session-{group.GroupReservationId}",
                GroupReservationId = group.GroupReservationId,
                GuestName = "Owner",
                Dishes = [],
                IsOwnerSession = true,
                OwnerUserId = userId
            };
            await guestSessionRepository.SaveOwnerIfAbsentAsync(ownerSession);
        }

        if (group.Status == GroupReservationStatus.Open)
        {
            await SyncOwnerSessionWithOrderAsync(ownerSession, reservation, userId);
        }

        return (ownerSession.Id, group.GroupReservationId);
    }

    private async Task SyncOwnerSessionWithOrderAsync(
        GuestSession ownerSession,
        Reservation reservation,
        string userId)
    {
        var order = await orderRepository.GetOrderByReservationIdAsync(reservation.Id);
        var activeOrder = order is not null && order.Status != OrderStatus.Cancelled ? order : null;

        ownerSession.GuestName = await userService.GetUserName(userId) ?? "Owner";
        ownerSession.OwnerUserId = userId;
        ownerSession.Dishes = activeOrder?.Dishes
            .Select(item => new OrderItem
            {
                DishId = item.DishId,
                Quantity = item.Quantity,
                State = item.State
            })
            .ToList() ?? [];
        ownerSession.IsSubmitted = activeOrder?.Status == OrderStatus.SubmittedByCustomer;

        await guestSessionRepository.SaveAsync(ownerSession);
    }

    public async Task<IReadOnlyList<ConsolidatedOrderItem>> SubmitGroupPreOrderAsync(
        string groupReservationId, string reservationId, string userId)
    {
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();
        var reservation = await GetOwnedReservationAsync(reservationId, userId);
        if (group.ReservationId != reservation.Id)
            throw new ValidationException("Reservation does not belong to this group reservation.");
        if (group.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();
        if (group.Status != GroupReservationStatus.Open || reservation.Status != ReservationStatus.Reserved)
            throw new GroupReservationExpiredException();

        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId);
        var submitted = sessions.Where(session => session.IsSubmitted).ToArray();
        if (submitted.Length == 0)
            throw new ValidationException("No submitted guest sessions were found.");

        var dishes = submitted.SelectMany(session => session.Dishes)
            .GroupBy(item => item.DishId)
            .Select(grouped => new ConsolidatedOrderItem(grouped.Key, grouped.Sum(item => item.Quantity)))
            .ToArray();
        if (dishes.Length == 0)
            throw new ValidationException("Submitted guest sessions contain no dishes.");

        var order = await orderRepository.GetOrderByReservationIdAsync(reservationId)
            ?? throw new ValidationException("Order was not found for this reservation.");
        order.Dishes = dishes.Select(item => new OrderItem
        {
            DishId = item.DishId,
            Quantity = item.Quantity,
            State = OrderItemState.Pending
        }).ToList();
        order.Status = OrderStatus.SubmittedByCustomer;
        await orderRepository.SaveOrderAsync(order);

        group.Status = GroupReservationStatus.Submitted;
        await groupReservationRepository.SaveGroupReservationAsync(group);
        return dishes;
    }

    private async Task<Reservation> GetOwnedReservationAsync(string reservationId, string userId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");
        if (reservation.CustomerId != userId)
            throw new ActionForbiddenException("Only the reservation owner can perform this action.");
        return reservation;
    }

    public async Task<GetGroupReservationResponse> GetGroupReservationByTokenAsync(string token)
    {
        GroupReservation groupReservation = await groupReservationRepository.GetGroupReservationByTokenAsync(token)
            ?? throw new GroupReservationNotFoundException();

        if (groupReservation.Status != GroupReservationStatus.Open)
            throw new GroupReservationExpiredException();

        Task<Reservation?> reservationTask = reservationRepository.GetReservationByIdAsync(groupReservation.ReservationId);
        Task<int> countTask = guestSessionRepository.CountJoinedGuestsAsync(groupReservation.GroupReservationId);

        Reservation reservation = await reservationTask ?? throw new ValidationException("Associated reservation not found.");
        int joinedGuestsCount = await countTask;

        if (joinedGuestsCount + 1 >= reservation.GuestCount)
            throw new GuestLimitExceededException();

        Table table = await tableRepository.GetTableAsync(reservation.TableId)
            ?? throw new InvalidOperationException("Associated table not found.");

        Location? location = await locationService.GetLocationByIdAsync(table.LocationId);
        string restaurantName = DefaultRestaurantName;
        string address = location?.Address ?? "Unknown Address";

        string inviterName = "Unknown";
        if (!string.IsNullOrWhiteSpace(reservation.CustomerId))
        {
            inviterName = await userService.GetUserName(reservation.CustomerId) ?? "Unknown";
        }
        return new GetGroupReservationResponse(
            groupReservation.GroupReservationId,
            table.LocationId,
            restaurantName,
            address,
            reservation.StartTime,
            reservation.EndTime,
            reservation.GuestCount,
            table.TableNumber,
            inviterName);
    }

    public async Task<GroupReservation> GetGroupReservationAsync(string groupReservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId);
        return groupReservation ?? throw new InvalidOperationException($"Group reservation with ID {groupReservationId} not found.");
    }

    public async Task<GroupReservation> CreateGroupReservationAsync(string reservationId)
    {
        var groupReservation = new GroupReservation
        {
            GroupReservationId = Guid.NewGuid().ToString(),
            ReservationId = reservationId,
            ReservationToken = Guid.NewGuid().ToString("N"),
            Status = GroupReservationStatus.Open,
        };

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
        return groupReservation;
    }

    public async Task CreateGroupReservationIfDoesNotExistAsync(string userId, string reservationId)
    {
        GroupReservation? group = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (group is not null)
            return;

        await CreateGroupReservationAsync(reservationId);
    }

    public async Task UpdateGroupReservationStatusAsync(string groupReservationId, GroupReservationStatus status)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        groupReservation.Status = status;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task DeleteGroupReservationAsync(string groupReservationId)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        await groupReservationRepository.DeleteGroupReservationAsync(groupReservation);
    }

    public async Task CloseGroupReservationAsync(string reservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (groupReservation is null)
            return;

        groupReservation.Status = GroupReservationStatus.Closed;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task<GetGroupInviteTokenResponse> GetGroupReservationInviteTokenAsync(string reservationId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");

        if (reservation.GuestCount < 2)
            throw new ValidationException("Retrieving invitation token for the reservation with only one guest is not permitted.");

        if (reservation.Status != ReservationStatus.Reserved)
            throw new GroupReservationExpiredException("The group reservation is expired.");

        GroupReservation groupReservation = (await groupReservationRepository.GetByReservationIdAsync(reservationId))!;

        if (groupReservation.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();

        if (groupReservation.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        return new GetGroupInviteTokenResponse(groupReservation.ReservationToken);
    }
}

