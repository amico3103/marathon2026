using Restaurant_Aqa_Framework.Screenplay.Abilities;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class ChangeGuestThroughUi(bool submit = false, string guestContext = "guest") : ITask
{
    public Task PerformAsAsync(Actor actor)
    {
        var menu = new GuestGroupMenuPage(actor.Recall<BrowseTheWeb>(guestContext).Page);
        return submit ? menu.SubmitAsync() : menu.IncreaseAsync();
    }
}
