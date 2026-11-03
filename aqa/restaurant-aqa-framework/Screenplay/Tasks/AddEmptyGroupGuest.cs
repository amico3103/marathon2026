using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class AddEmptyGroupGuest : ITask
{
    public Task PerformAsAsync(Actor actor) => actor.Recall<OwnedGroupData>()
        .AddGuestAsync("AQA-Sample-Empty", 0, submitted: false);
}
