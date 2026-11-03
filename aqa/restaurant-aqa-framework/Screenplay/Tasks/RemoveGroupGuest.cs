using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed record RemovalSelection(string SessionId, string Name);

public sealed class RemoveGroupGuest(string choice = "confirmed guest", string requester = "owner", bool throughUi = false, bool cancel = false, string? guestName = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var data = actor.Recall<OwnedGroupData>();
        if (choice == "foreign guest" && actor.Recall<OwnedGroupData>("other-group").GroupId == data.GroupId)
            throw new InvalidOperationException("The cross-group test requires two different groups.");
        var selection = guestName is not null ? await FindGuestAsync(data, guestName) : choice switch
        {
            "owner" => new RemovalSelection(data.OwnerSessionId, "Owner"),
            "missing guest" => new RemovalSelection(Guid.NewGuid().ToString(), "Missing"),
            "foreign guest" => FromGuest(actor.Recall<OwnedGroupData>("other-group").Guests[0]),
            "confirmed guest" => FromGuest(data.Guests.First(guest => guest.Submitted)),
            "unconfirmed guest" => FromGuest(data.Guests.First(guest => !guest.Submitted)),
            _ => throw new InvalidOperationException("Unsupported removal target."),
        };
        actor.Remember(selection);
        actor.Remember("before-submit", await actor.AsksForAsync(new TheGroupSubmissionState()));
        if (throughUi)
        {
            actor.Remember("removal-dialog", await new GroupOrderPage(actor.LookingAtThePage()).RemoveGuestAsync(selection.Name, cancel));
            return;
        }
        var token = await RequestParticipants.ResolveTokenAsync(actor, requester);
        actor.Remember(new ParticipantRequester(requester));
        await actor.TheLastResponse().CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi())
            .RemoveGuestAsync(data.GroupId, selection.SessionId, token));
    }

    private static RemovalSelection FromGuest(GroupGuest guest) => new(guest.SessionId, guest.Name);

    private static async Task<RemovalSelection> FindGuestAsync(OwnedGroupData data, string name)
    {
        var participant = (await data.GetSnapshotAsync()).GetProperty("participants").EnumerateArray()
            .Single(item => item.GetProperty("type").GetString() == "Guest" && item.GetProperty("name").GetString() == name);
        return new(participant.GetProperty("id").GetString()!, name);
    }
}

public sealed class PrepareSingleRemovalGuest(bool withoutOwner = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        await actor.AttemptsToAsync(new PrepareGroupReview(withoutOwner ? GroupDataShape.EmptyGroup : GroupDataShape.Review));
        if (withoutOwner)
        {
            await actor.Recall<OwnedGroupData>().AddGuestAsync("AQA-Sample-Alex", 1, submitted: true);
            return;
        }
        await actor.AttemptsToAsync(new RemoveGroupGuest("unconfirmed guest"));
        var outcome = await actor.AsksForAsync(new TheOutcome());
        if (!outcome.Succeeded) throw new InvalidOperationException("Cannot prepare a single-guest group: " + outcome.Body);
    }
}

public sealed class PrepareOtherRemovalGroup : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var current = actor.Recall<OwnedGroupData>();
        var other = await OwnedGroupData.CreateAsync(actor.Recall<IAPIRequestContext>("fixture-api"));
        actor.Recall<CleanupScope>().TrackResource(other);
        actor.Remember("other-group", other);
        // A named Remember also makes that object current; keep the original group as the actor's target.
        actor.Remember(current);
        actor.Remember("other-group-before", await actor.AsksForAsync(new TheGroupSubmissionState("other-group")));
    }
}

public sealed class CloseRemovalGroup(string state) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        if (state == "submitted") await actor.AttemptsToAsync(new SubmitGroupOrder());
        else if (state == "closed")
        {
            var data = actor.Recall<OwnedGroupData>();
            await actor.TheLastResponse().CaptureAsync(await new ReservationsApi(actor.Recall<IAPIRequestContext>("fixture-api"))
                .CancelReservationAsync(data.Reservation.Id, data.OwnerToken));
        }
        else throw new InvalidOperationException("Unsupported closed-group state.");
        var outcome = await actor.AsksForAsync(new TheOutcome());
        if (!outcome.Succeeded) throw new InvalidOperationException("Cannot prepare the locked group: " + outcome.Body);
        actor.Remember("locked-group-status", state == "submitted" ? "Submitted" : "Closed");
    }
}
