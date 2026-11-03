using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using System.Text.Json;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupParticipantStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("a customer with a private table")]
    public Task GivenPrivateTable() => Actor.AttemptsToAsync(new PrepareGroupReview(GroupDataShape.CustomerAndTable));

    [Given("a group with confirmed and unconfirmed carts")]
    public Task GivenReview() => Actor.AttemptsToAsync(new PrepareGroupReview(GroupDataShape.Review));

    [Given("a group with different dishes in its carts")]
    public Task GivenMixedReview() => Actor.AttemptsToAsync(new PrepareGroupReview(GroupDataShape.MixedReview));

    [Given("a group without any participant sessions")]
    public Task GivenEmptyGroup() => Actor.AttemptsToAsync(new PrepareGroupReview(GroupDataShape.EmptyGroup));

    [Given("a group containing only the owner")]
    public Task GivenOwnerOnly() => Actor.AttemptsToAsync(new PrepareGroupReview(GroupDataShape.OwnerOnly));

    [Given("a guest without dishes has joined the group")]
    public Task GivenEmptyGuest() => Actor.AttemptsToAsync(new AddEmptyGroupGuest());

    [Given("the owner is reviewing the group")]
    [When("the owner opens the group review")]
    public Task WhenOwnerOpensReview() => Actor.AttemptsToAsync(new OpenGroupReview());

    [Given("a guest has joined and chosen a dish")]
    public async Task GivenJoinedGuest()
    {
        await Actor.AttemptsToAsync(new InviteGuestThroughUi());
        await Actor.AsksForAsync(new TheParticipantCart("Guest: " + InviteGuestThroughUi.VisitorName, waitForQuantity: 1));
    }

    [When("the owner books the table and invites a guest who chooses a dish")]
    public Task WhenBookAndJoin() => Actor.AttemptsToAsync(new InviteGuestThroughUi(bookThroughUi: true));

    [When("the guest increases their dish quantity")]
    public Task WhenGuestChangesCart() => Actor.AttemptsToAsync(new ChangeGuestThroughUi());

    [When("the guest confirms their cart")]
    public Task WhenGuestSubmits() => Actor.AttemptsToAsync(new ChangeGuestThroughUi(submit: true));

    [When("the participant list is requested by {string}")]
    public Task WhenParticipantsRequested(string requester) => Actor.AttemptsToAsync(new RequestParticipants(requester));

    [When("the owner requests participants of a group that does not exist")]
    public Task WhenMissingRequested() => Actor.AttemptsToAsync(new RequestParticipants("owner", missing: true));

    [When("the owner opens a group review that does not exist")]
    public Task WhenMissingOpened() => Actor.AttemptsToAsync(new OpenGroupReview(missing: true));

    [Then("the owner sees the invited guest and their chosen dish")]
    public async Task ThenInvitedGuestVisible()
    {
        var data = Recall<OwnedGroupData>();
        await CheckCartAsync(await Actor.AsksForAsync(new TheParticipantCart("Guest: " + InviteGuestThroughUi.VisitorName, 1)), data.Dish, 1, false);
        await Assert.That(await Actor.AsksForAsync(new TheGroupCardCount())).IsEqualTo(2);
        await Assert.That(await Actor.AsksForAsync(new TheGroupGrandTotal())).IsEqualTo(0m);
    }

    [Then("every participant has the correct dishes, quantities and subtotal")]
    public async Task ThenAllCartsCorrect()
    {
        var data = Recall<OwnedGroupData>();
        await CheckCartAsync(await Actor.AsksForAsync(new TheParticipantCart("My Pre-Order")), data.Dish, 1, true);
        foreach (var guest in data.Guests)
            await CheckCartAsync(await Actor.AsksForAsync(new TheParticipantCart("Guest: " + guest.Name)), guest.Dish, guest.Quantity, guest.Submitted);
        await Assert.That(await Actor.AsksForAsync(new TheGroupCardCount())).IsEqualTo(3);
        var snapshot = await Actor.AsksForAsync(new TheApiGroupSnapshot());
        var expectedAll = data.Dish.Price + data.Guests.Sum(guest => guest.Dish.Price * guest.Quantity);
        await Assert.That(snapshot.GetProperty("total").GetDecimal()).IsEqualTo(expectedAll).Because($"snapshot: {snapshot}");
    }

    [Then("the owner sees the changed quantity without reloading")]
    public async Task ThenQuantityUpdates()
    {
        var data = Recall<OwnedGroupData>();
        await CheckCartAsync(await Actor.AsksForAsync(new TheParticipantCart("Guest: " + InviteGuestThroughUi.VisitorName, 2)), data.Dish, 2, false);
        await Assert.That(await Actor.AsksForAsync(new TheGroupGrandTotal())).IsEqualTo(0m);
        var snapshot = await Actor.AsksForAsync(new TheApiGroupSnapshot());
        await Assert.That(snapshot.GetProperty("total").GetDecimal()).IsEqualTo(data.Dish.Price * 2).Because($"snapshot: {snapshot}");
    }

    [Then("the owner sees the guest cart confirmed and included in the grand total")]
    public async Task ThenGuestConfirmed()
    {
        var data = Recall<OwnedGroupData>();
        await CheckCartAsync(await Actor.AsksForAsync(new TheParticipantCart("Guest: " + InviteGuestThroughUi.VisitorName,
            waitForSubmission: true)), data.Dish, 1, true);
        await Assert.That(await Actor.AsksForAsync(new TheGroupGrandTotal())).IsEqualTo(data.Dish.Price);
    }

    [Then("the unconfirmed cart is visible but excluded from the grand total")]
    public async Task ThenPendingExcluded()
    {
        var data = Recall<OwnedGroupData>();
        var guest = data.Guests.Single(guest => !guest.Submitted);
        await CheckCartAsync(await Actor.AsksForAsync(new TheParticipantCart("Guest: " + guest.Name)), guest.Dish, guest.Quantity, false);
        await Assert.That(await Actor.AsksForAsync(new TheGroupNotice())).Contains("1 participant hasn't submitted");
        await Assert.That(await Actor.AsksForAsync(new TheGroupGrandTotal()))
            .IsEqualTo(data.Dish.Price + data.Guests.Where(guest => guest.Submitted).Sum(guest => guest.Dish.Price * guest.Quantity));
    }

    [Then("the guest without dishes remains visible with a zero subtotal")]
    public async Task ThenEmptyGuestVisible()
    {
        var cart = await Actor.AsksForAsync(new TheParticipantCart("Guest: AQA-Sample-Empty"));
        await Assert.That(cart.Dishes.Count).IsEqualTo(0).Because(cart.Text);
        await Assert.That(cart.Subtotal).IsEqualTo(0m);
        await Assert.That(cart.Submitted).IsFalse();
        await Assert.That(cart.Text).Contains("No dishes yet");
    }

    [Then("the group has no participants and a zero total")]
    public async Task ThenApiEmpty()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(200).Because(outcome.Body);
        var snapshot = JsonSerializer.Deserialize<JsonElement>(outcome.Body);
        await Assert.That(snapshot.GetProperty("participants").GetArrayLength()).IsEqualTo(0);
        await Assert.That(snapshot.GetProperty("total").GetDecimal()).IsEqualTo(0m);
    }

    [Then("only the owner's empty cart is displayed")]
    public async Task ThenOwnerOnly()
    {
        var cart = await Actor.AsksForAsync(new TheParticipantCart("My Pre-Order"));
        await Assert.That(await Actor.AsksForAsync(new TheGroupCardCount())).IsEqualTo(1);
        await Assert.That(cart.Dishes.Count).IsEqualTo(0).Because(cart.Text);
        await Assert.That(cart.Subtotal).IsEqualTo(0m);
        await Assert.That(cart.Text).Contains("No dishes yet");
        await Assert.That(await Actor.AsksForAsync(new TheGroupGrandTotal())).IsEqualTo(0m);
    }

    [Then("the owner sees that the group cannot be found and can retry")]
    public async Task ThenMissingGroupVisible()
    {
        var message = await Actor.AsksForAsync(new TheMissingGroupMessage());
        await Assert.That(message).Contains("Group reservation not found");
        await Assert.That(message).Contains("Try again");
    }

    internal static async Task CheckCartAsync(ParticipantCartView cart, DishListResponse dish, int quantity, bool submitted)
    {
        await Assert.That(cart.Dishes.Count).IsEqualTo(1).Because(cart.Text);
        await Assert.That(cart.Dishes[0].Name).IsEqualTo(dish.Name).Because(cart.Text);
        await Assert.That(cart.Dishes[0].Quantity).IsEqualTo(quantity).Because(cart.Text);
        await Assert.That(cart.Dishes[0].Total).IsEqualTo(dish.Price * quantity).Because(cart.Text);
        await Assert.That(cart.Subtotal).IsEqualTo(dish.Price * quantity).Because(cart.Text);
        await Assert.That(cart.Submitted).IsEqualTo(submitted).Because(cart.Text);
    }
}
