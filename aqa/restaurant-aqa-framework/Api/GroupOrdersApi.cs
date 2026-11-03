using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Api;

public sealed class GroupOrdersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> SubmitAsync(string groupId, string reservationId, string? token) =>
        Api.PostAsync(Url($"/group-reservations/{groupId}/submit"), new()
        {
            DataString = Serialize(new { reservationId }),
            Headers = Headers(token),
        });

    public Task<IAPIResponse> GetStatusAsync(string groupId) => GetAsync(Url($"/guest/groups/{groupId}/status"));

    public Task<IAPIResponse> RemoveGuestAsync(string groupId, string sessionId, string? token) =>
        DeleteAsync(Url($"/guest/groups/{groupId}/sessions/{sessionId}"), new() { Headers = Headers(token) });

    public Task<IAPIResponse> GetOwnerSessionAsync(string reservationId, string token) =>
        GetAsync(Url($"/reservations/{reservationId}/guest-session-id"), new() { Headers = Headers(token) });

    public Task<IAPIResponse> GetSessionsAsync(string groupId, string? token = null) =>
        GetAsync(Url($"/guest/groups/{groupId}/sessions"), new() { Headers = Headers(token) });

    public Task<IAPIResponse> GetInviteTokenAsync(string reservationId, string token) =>
        GetAsync(Url($"/reservations/{reservationId}/invite-token"), new() { Headers = Headers(token) });

    public Task<IAPIResponse> GetInviteAsync(string token) => GetAsync(Url($"/guest/invites/{token}"));

    public Task<IAPIResponse> CreateGuestAsync(string groupId, string name) =>
        Api.PostAsync(Url("/guest/sessions"), new()
        {
            DataString = Serialize(new { groupReservationId = groupId, guestName = name }),
            Headers = Headers(),
        });

    public Task<IAPIResponse> AddDishAsync(string sessionId, string token, string dishId, int quantity) =>
        Api.PostAsync(Url($"/guest/sessions/{sessionId}/dishes"), new()
        {
            DataString = Serialize(new { dishId, quantity }),
            Headers = Headers(token),
        });

    public Task<IAPIResponse> SubmitGuestAsync(string sessionId, string token) =>
        Api.PostAsync(Url($"/guest/sessions/{sessionId}/submit"), new() { Headers = Headers(token) });
}
