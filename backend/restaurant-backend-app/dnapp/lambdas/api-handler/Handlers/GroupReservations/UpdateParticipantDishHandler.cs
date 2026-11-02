using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

public class UpdateParticipantDishHandler(IGroupReservationService groups, IGuestSessionService sessions) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var userId = request.RequestContext is null ? null : UserContextHelper.GetAuthenticatedUserId(request);
        if (userId is null) return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);
        if (!request.TryGetPathValue("groupReservationId", out var groupId) ||
            !request.TryGetPathValue("guestSessionId", out var sessionId))
            return ResponseBuilder.CreateErrorResponse(400, "Group and session IDs are required.", context.Logger);
        try
        {
            using var body = JsonDocument.Parse(request.Body ?? "");
            if (body.RootElement.ValueKind != JsonValueKind.Object || !body.RootElement.TryGetProperty("dishId", out var dish) || dish.ValueKind != JsonValueKind.String ||
                !body.RootElement.TryGetProperty("quantity", out var quantity) || quantity.ValueKind != JsonValueKind.Number || !quantity.TryGetInt32(out var count))
                return ResponseBuilder.CreateErrorResponse(400, "Dish ID and integer quantity are required.", context.Logger);
            var edited = await groups.UpdateParticipantDishAsync(userId, groupId, sessionId, dish.GetString()!, count);
            var snapshot = await sessions.GetEditedGroupSessionsAsync(userId, groupId, edited);
            return ResponseBuilder.CreateJsonResponse(200, snapshot);
        }
        catch (JsonException) { return ResponseBuilder.CreateErrorResponse(400, "Invalid request body.", context.Logger); }
        catch (ActionForbiddenException exception) { return ResponseBuilder.CreateErrorResponse(403, exception.Message, context.Logger); }
        catch (GroupReservationExpiredException exception) { return ResponseBuilder.CreateErrorResponse(409, exception.Message, context.Logger); }
        catch (GroupReservationNotFoundException exception) { return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger); }
        catch (ReservationNotFoundException exception) { return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger); }
        catch (GuestSessionNotFoundException exception) { return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger); }
        catch (DishNotFoundException exception) { return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger); }
        catch (ValidationException exception) { return ResponseBuilder.CreateErrorResponse(400, exception.Message, context.Logger); }
    }
}
