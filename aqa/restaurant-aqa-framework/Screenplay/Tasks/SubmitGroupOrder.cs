using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class SubmitGroupOrder(string requester = "owner", string target = "current", bool throughUi = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var data = actor.Recall<OwnedGroupData>();
        actor.Remember("before-submit", await actor.AsksForAsync(new TheGroupSubmissionState()));
        if (throughUi)
        {
            await new GroupOrderPage(actor.LookingAtThePage()).SubmitAsync();
            return;
        }
        var token = await RequestParticipants.ResolveTokenAsync(actor, requester);
        actor.Remember(new ParticipantRequester(requester));
        await actor.TheLastResponse().CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi()).SubmitAsync(
            target == "missing group" ? Guid.NewGuid().ToString() : data.GroupId,
            target == "missing reservation" ? Guid.NewGuid().ToString() : data.Reservation.Id, token));
    }
}
