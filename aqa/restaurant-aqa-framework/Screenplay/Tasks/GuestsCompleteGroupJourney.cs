using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed record JourneyMenu(DishListResponse RemovedDish);

public sealed class GuestsCompleteGroupJourney : ITask
{
    public const string RetainedName = "AQA-Sample-Alex";
    public const string RemovedName = "AQA-Sample-Maria";

    public async Task PerformAsAsync(Actor actor)
    {
        var data = actor.Recall<OwnedGroupData>();
        var response = await new DishesApi(actor.Recall<IAPIRequestContext>("fixture-api"))
            .GetDishesAsync(reservationId: data.Reservation.Id);
        if (!response.Ok) throw new InvalidOperationException("Cannot read the journey reservation menu.");
        var menu = await response.ReadAsAsync<List<DishListResponse>>();
        var removedDish = menu.FirstOrDefault(dish => dish.State == "Available" && dish.Id != data.Dish.Id && dish.Name != data.Dish.Name)
            ?? throw new InvalidOperationException("The journey requires two distinguishable available dishes.");
        actor.Remember(new JourneyMenu(removedDish));
        await actor.AttemptsToAsync(
            new InviteGuestThroughUi(guestName: RetainedName, guestContext: "guest-retained"),
            new ChangeGuestThroughUi(guestContext: "guest-retained"),
            new ChangeGuestThroughUi(submit: true, guestContext: "guest-retained"),
            new InviteGuestThroughUi(guestName: RemovedName, dishName: removedDish.Name, guestContext: "guest-removed"),
            new ChangeGuestThroughUi(submit: true, guestContext: "guest-removed"));
    }
}
