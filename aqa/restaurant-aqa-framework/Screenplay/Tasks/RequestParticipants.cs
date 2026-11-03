using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed record ParticipantRequester(string Name);

public sealed class RequestParticipants(string requester, bool missing = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var data = actor.Recall<OwnedGroupData>();
        var token = await ResolveTokenAsync(actor, requester);
        actor.Remember(new ParticipantRequester(requester));
        await actor.TheLastResponse().CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi())
            .GetSessionsAsync(missing ? Guid.NewGuid().ToString() : data.GroupId, token));
    }

    internal static async Task<string?> ResolveTokenAsync(Actor actor, string requester)
    {
        var data = actor.Recall<OwnedGroupData>();
        string? token;
        switch (requester)
        {
            case "owner": token = data.OwnerToken; break;
            case "waiter": token = data.WaiterToken; break;
            case "guest": token = data.Guests[0].Token; break;
            case "anonymous visitor": token = null; break;
            case "another customer":
                var stranger = await OwnedGroupData.CreateAsync(actor.Recall<IAPIRequestContext>("fixture-api"), GroupDataShape.CustomerAndTable);
                actor.Recall<CleanupScope>().TrackResource(stranger);
                token = stranger.OwnerToken;
                break;
            default: throw new InvalidOperationException("Unsupported participant requester.");
        }
        return token;
    }
}
