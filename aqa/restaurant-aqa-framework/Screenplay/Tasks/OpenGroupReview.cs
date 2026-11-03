using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class OpenGroupReview(bool missing = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var data = actor.Recall<OwnedGroupData>();
        await actor.AttemptsToAsync(new SignInThroughTheLoginForm(data.OwnerEmail, data.OwnerPassword));
        var page = new GroupOrderPage(actor.LookingAtThePage());
        if (missing) await page.OpenMissingAsync(Guid.NewGuid().ToString());
        else await page.OpenAsync(data.Reservation.Id);
    }
}
