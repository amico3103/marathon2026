using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using System.Globalization;
using System.Text.Json;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupJourneyStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("a private group with an initial owner pre-order")]
    public Task GivenOwnerPreOrder() => Actor.AttemptsToAsync(new PrepareGroupReview(GroupDataShape.OwnerPreOrder));

    [When("two invited guests choose different dishes and confirm their carts")]
    public Task WhenGuestsOrder() => Actor.AttemptsToAsync(new GuestsCompleteGroupJourney());

    [When("the owner removes the guest with the unique dish")]
    public Task WhenRemoveUniqueDishGuest() => Actor.AttemptsToAsync(new RemoveGroupGuest(
        throughUi: true, guestName: GuestsCompleteGroupJourney.RemovedName));

    [Then("the owner sees both confirmed guest carts and the correct combined amounts")]
    public async Task ThenAllCartsVisible()
    {
        var data = Recall<OwnedGroupData>();
        var removedDish = Recall<JourneyMenu>().RemovedDish;
        await CheckRetainedCartsAsync();
        await GroupParticipantStepDefinitions.CheckCartAsync(await Actor.AsksForAsync(new TheParticipantCart(
            "Guest: " + GuestsCompleteGroupJourney.RemovedName)), removedDish, 1, true);
        await Assert.That(await Actor.AsksForAsync(new TheGroupCardCount())).IsEqualTo(3);
        var total = data.Dish.Price * 3 + removedDish.Price;
        var snapshot = await Actor.AsksForAsync(new TheApiGroupSnapshot());
        await Assert.That(snapshot.GetProperty("participants").GetArrayLength()).IsEqualTo(3).Because(snapshot.ToString());
        await Assert.That(snapshot.GetProperty("participants").EnumerateArray()
            .All(item => item.GetProperty("isSubmitted").GetBoolean())).IsTrue().Because(snapshot.ToString());
        await Assert.That(snapshot.GetProperty("total").GetDecimal()).IsEqualTo(total).Because(snapshot.ToString());
        await Assert.That(await Actor.AsksForAsync(new TheGroupGrandTotal())).IsEqualTo(total);
        var screen = await Actor.AsksForAsync(new TheGroupSubmissionScreen());
        await Assert.That(screen.Rows.Count).IsEqualTo(2).Because(screen.ToString());
        await Assert.That(screen.Rows.Any(row => row.Contains(data.Dish.Name + " × 3", StringComparison.Ordinal))).IsTrue().Because(screen.ToString());
        await Assert.That(screen.Rows.Any(row => row.Contains(removedDish.Name + " × 1", StringComparison.Ordinal))).IsTrue().Because(screen.ToString());
    }

    [Then("the retained participants and their amounts remain correct")]
    public async Task ThenRetainedCarts()
    {
        var selection = Recall<RemovalSelection>();
        await Assert.That(selection.Name).IsEqualTo(GuestsCompleteGroupJourney.RemovedName);
        await Assert.That(Recall<string>("removal-dialog")).Contains(selection.Name);
        var before = JsonSerializer.Deserialize<JsonElement>(Recall<GroupSubmissionState>("before-submit").Participants);
        var snapshot = await Actor.AsksForAsync(new TheApiGroupSnapshot());
        var participants = snapshot.GetProperty("participants").EnumerateArray().ToArray();
        await Assert.That(participants.Length).IsEqualTo(2).Because(snapshot.ToString());
        await Assert.That(participants.Any(item => item.GetProperty("id").GetString() == selection.SessionId)).IsFalse().Because(snapshot.ToString());
        foreach (var participant in participants)
        {
            var original = before.GetProperty("participants").EnumerateArray()
                .Single(item => item.GetProperty("id").GetString() == participant.GetProperty("id").GetString());
            await Assert.That(JsonElement.DeepEquals(participant, original)).IsTrue().Because(participant.ToString());
        }
        await CheckRetainedCartsAsync();
        await Assert.That(await Actor.AsksForAsync(new TheGroupCardCount())).IsEqualTo(2);
        var total = Recall<OwnedGroupData>().Dish.Price * 3;
        await Assert.That(snapshot.GetProperty("total").GetDecimal()).IsEqualTo(total).Because(snapshot.ToString());
        await Assert.That(await Actor.AsksForAsync(new TheGroupGrandTotal())).IsEqualTo(total);
        var screen = await Actor.AsksForAsync(new TheGroupSubmissionScreen());
        await Assert.That(screen.Rows.Count).IsEqualTo(1).Because(screen.ToString());
        await Assert.That(screen.Rows[0]).Contains(Recall<OwnedGroupData>().Dish.Name + " × 3");
        await Assert.That(screen.Rows[0]).Contains(total.ToString("C", CultureInfo.GetCultureInfo("en-US")));
    }

    [Then("the removed guest's unique dish is absent from the restaurant order")]
    public async Task ThenUniqueDishAbsent()
    {
        var removedDish = Recall<JourneyMenu>().RemovedDish;
        var state = await Actor.AsksForAsync(new TheGroupSubmissionState());
        var order = JsonSerializer.Deserialize<GetOrderResponse>(state.OrderBody, JsonDefaults.Options)!;
        await Assert.That(order.OrderItems.Any(item => item.Dish.Id == removedDish.Id)).IsFalse().Because(state.OrderBody);
        var waiterDishes = await Actor.AsksForAsync(new TheWaiterGroupDishes());
        await Assert.That(waiterDishes.Any(item => item.Name == removedDish.Name)).IsFalse().Because(string.Join(", ", waiterDishes));
    }

    private async Task CheckRetainedCartsAsync()
    {
        var dish = Recall<OwnedGroupData>().Dish;
        await GroupParticipantStepDefinitions.CheckCartAsync(await Actor.AsksForAsync(new TheParticipantCart("My Pre-Order")), dish, 1, true);
        await GroupParticipantStepDefinitions.CheckCartAsync(await Actor.AsksForAsync(new TheParticipantCart(
            "Guest: " + GuestsCompleteGroupJourney.RetainedName)), dish, 2, true);
    }
}
