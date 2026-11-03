using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using System.Text.Json;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupSubmissionStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("a group where the owner and both guests have confirmed the same dish")]
    public Task GivenAllConfirmed() => Actor.AttemptsToAsync(new PrepareGroupReview(GroupDataShape.AllConfirmed));

    [Given("a group with only unconfirmed choices")]
    public Task GivenPending() => Actor.AttemptsToAsync(new PrepareGroupReview(GroupDataShape.PendingOnly));

    [Given("an isolated group with a confirmed but empty cart")]
    public Task GivenEmptySubmitted() => Actor.AttemptsToAsync(new PrepareGroupReview(GroupDataShape.SubmittedEmpty));

    [Given("a group with a confirmed guest cart but no owner order")]
    public Task GivenNoOrder() => Actor.AttemptsToAsync(new PrepareGroupReview(GroupDataShape.NoOrder));

    [When("the owner confirms the consolidated pre-order")]
    [Given("the owner confirms the consolidated pre-order")]
    public Task WhenSubmitUi() => Actor.AttemptsToAsync(new SubmitGroupOrder(throughUi: true));

    [Given("the owner has already sent the consolidated pre-order")]
    public async Task GivenSubmitted()
    {
        await Actor.AttemptsToAsync(new SubmitGroupOrder());
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        if (!outcome.Succeeded) throw new InvalidOperationException("Could not prepare submitted group: " + outcome.Body);
    }

    [When("the owner attempts to send the pre-order through the service")]
    public Task WhenSubmitApi() => Actor.AttemptsToAsync(new SubmitGroupOrder());

    [When("the owner attempts to send the group for a reservation that does not exist")]
    public Task WhenMissingReservation() => Actor.AttemptsToAsync(new SubmitGroupOrder(target: "missing reservation"));

    [When("the owner attempts to send a group that does not exist")]
    public Task WhenMissingGroup() => Actor.AttemptsToAsync(new SubmitGroupOrder(target: "missing group"));

    [When("the pre-order is submitted by {string}")]
    public Task WhenOtherSubmits(string requester) => Actor.AttemptsToAsync(new SubmitGroupOrder(requester));

    [When("the assigned waiter opens the reservation pre-order")]
    public Task WhenWaiterOpens() => Actor.AttemptsToAsync(new OpenWaiterGroupOrder());

    [Then("the owner has their own session in this group")]
    public async Task ThenOwnerSession()
    {
        var data = Recall<OwnedGroupData>();
        var session = await Actor.AsksForAsync(new TheOwnerSession());
        await Assert.That(session.GetProperty("groupReservationId").GetString()).IsEqualTo(data.GroupId).Because(session.ToString());
        await Assert.That(session.GetProperty("guestSessionId").GetString()).IsEqualTo(data.OwnerSessionId).Because(session.ToString());
        var cart = await Actor.AsksForAsync(new TheParticipantCart("My Pre-Order"));
        await Assert.That(cart.Text).Contains("My Pre-Order");
        var snapshot = await Actor.AsksForAsync(new TheApiGroupSnapshot());
        await Assert.That(snapshot.GetProperty("participants").EnumerateArray()
            .Any(item => item.GetProperty("id").GetString() == data.OwnerSessionId)).IsTrue().Because(snapshot.ToString());
    }

    [Then("the pre-order is sent to the restaurant and cannot be edited")]
    public async Task ThenReadOnly()
    {
        await CheckOrderAsync(3);
        var screen = await Actor.AsksForAsync(new TheGroupSubmissionScreen());
        await Assert.That(screen.Badge.Trim()).IsEqualTo("Sent to Restaurant").Because(screen.ToString());
        await Assert.That(screen.Message).Contains("Pre-order sent to the restaurant");
        await Assert.That(screen.Disabled).IsTrue().Because(screen.ToString());
        await Assert.That(screen.EditControls).IsEqualTo(0).Because(screen.ToString());
        await Assert.That(screen.RemoveControls).IsEqualTo(0).Because(screen.ToString());
    }

    [Then("the restaurant order contains four portions in one dish entry")]
    public Task ThenCombined() => CheckOrderAsync(4);

    [Then("the restaurant order contains only the confirmed portions")]
    public Task ThenConfirmedOnly() => CheckOrderAsync(3);

    [Then("the waiter sees the dish and quantity sent by the owner")]
    public async Task ThenWaiterSeesOrder()
    {
        await CheckOrderAsync(3);
        var dishes = await Actor.AsksForAsync(new TheWaiterGroupDishes());
        await Assert.That(dishes.Count).IsEqualTo(1).Because(string.Join(", ", dishes));
        await Assert.That(dishes[0].Name).IsEqualTo(Recall<OwnedGroupData>().Dish.Name).Because(dishes[0].ToString());
        await Assert.That(dishes[0].Quantity.Trim()).IsEqualTo("Quantity: 3").Because(dishes[0].ToString());
    }

    [Then("sending the pre-order is unavailable")]
    public async Task ThenSubmitUnavailable()
    {
        var screen = await Actor.AsksForAsync(new TheGroupSubmissionScreen());
        await Assert.That(screen.Disabled).IsTrue().Because(screen.ToString());
        await Assert.That(screen.Badge.Trim()).IsEqualTo("Collecting pre-orders").Because(screen.ToString());
    }

    [Then("the group and order remain unchanged")]
    public async Task ThenUnchanged()
    {
        var before = Recall<GroupSubmissionState>("before-submit");
        var after = await Actor.AsksForAsync(new TheGroupSubmissionState());
        await CheckUnchangedAsync(before, after);
    }

    internal static async Task CheckUnchangedAsync(GroupSubmissionState before, GroupSubmissionState after)
    {
        await Assert.That(after.GroupStatus).IsEqualTo(before.GroupStatus).Because(after.ToString());
        await Assert.That(after.OrderStatusCode).IsEqualTo(before.OrderStatusCode).Because(after.ToString());
        await Assert.That(JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(after.OrderBody),
            JsonSerializer.Deserialize<JsonElement>(before.OrderBody))).IsTrue().Because(after.OrderBody);
        await Assert.That(JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(after.Participants),
            JsonSerializer.Deserialize<JsonElement>(before.Participants))).IsTrue().Because(after.Participants);
    }

    private async Task CheckOrderAsync(int quantity)
    {
        var data = Recall<OwnedGroupData>();
        var state = await Actor.AsksForAsync(new TheGroupSubmissionState());
        await Assert.That(state.GroupStatus).IsEqualTo("Submitted").Because(state.ToString());
        await Assert.That(state.OrderStatusCode).IsEqualTo(200).Because(state.OrderBody);
        var order = JsonSerializer.Deserialize<GetOrderResponse>(state.OrderBody, JsonDefaults.Options)!;
        await Assert.That(order.ReservationId).IsEqualTo(data.Reservation.Id).Because(state.OrderBody);
        await Assert.That(order.Status).IsEqualTo(OrderStatus.SubmittedByCustomer).Because(state.OrderBody);
        await Assert.That(order.OrderItems.Count).IsEqualTo(1).Because(state.OrderBody);
        await Assert.That(order.OrderItems[0].Dish.Id).IsEqualTo(data.Dish.Id).Because(state.OrderBody);
        await Assert.That(order.OrderItems[0].Quantity).IsEqualTo(quantity).Because(state.OrderBody);
        await Assert.That(order.OrderItems[0].Dish.Price * quantity).IsEqualTo(data.Dish.Price * quantity).Because(state.OrderBody);
        var screen = await Actor.AsksForAsync(new TheGroupSubmissionScreen());
        await Assert.That(screen.Rows.Count).IsEqualTo(1).Because(screen.ToString());
        await Assert.That(screen.Rows[0]).Contains(data.Dish.Name + " × " + quantity);
        await Assert.That(await Actor.AsksForAsync(new TheGroupGrandTotal())).IsEqualTo(data.Dish.Price * quantity);
    }
}
