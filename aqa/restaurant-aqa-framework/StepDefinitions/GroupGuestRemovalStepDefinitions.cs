using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using System.Globalization;
using System.Text.Json;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupGuestRemovalStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("a group with the owner and one confirmed guest")]
    public Task GivenSingleGuest() => Actor.AttemptsToAsync(new PrepareSingleRemovalGuest());

    [Given("a group with one confirmed guest and no owner session")]
    public Task GivenNoOwnerSession() => Actor.AttemptsToAsync(new PrepareSingleRemovalGuest(withoutOwner: true));

    [Given("another independent group with its own guests")]
    public Task GivenOtherGroup() => Actor.AttemptsToAsync(new PrepareOtherRemovalGroup());

    [Given("the group is already {string}")]
    public Task GivenLocked(string state) => Actor.AttemptsToAsync(new CloseRemovalGroup(state));

    [When("the owner removes the {string} from the review")]
    public Task WhenRemoveUi(string choice) => Actor.AttemptsToAsync(new RemoveGroupGuest(choice, throughUi: true));

    [When("the owner cancels removal of the confirmed guest")]
    public Task WhenCancelRemoval() => Actor.AttemptsToAsync(new RemoveGroupGuest(throughUi: true, cancel: true));

    [When("the owner requests removal of the {string}")]
    public Task WhenRemoveApi(string choice) => Actor.AttemptsToAsync(new RemoveGroupGuest(choice));

    [When("removal of a confirmed guest is requested by {string}")]
    public Task WhenOtherRemoves(string requester) => Actor.AttemptsToAsync(new RemoveGroupGuest(requester: requester));

    [Then("only the selected guest disappears and all remaining totals are correct")]
    public async Task ThenOnlySelectedRemoved()
    {
        var selection = Recall<RemovalSelection>();
        await Assert.That(Recall<string>("removal-dialog")).Contains(selection.Name);
        var before = Recall<GroupSubmissionState>("before-submit");
        var beforeSnapshot = JsonSerializer.Deserialize<JsonElement>(before.Participants);
        var participants = beforeSnapshot.GetProperty("participants").EnumerateArray().ToArray();
        var removed = participants.Single(item => item.GetProperty("id").GetString() == selection.SessionId);
        var after = await Actor.AsksForAsync(new TheGroupSubmissionState());
        var snapshot = JsonSerializer.Deserialize<JsonElement>(after.Participants);
        var remaining = snapshot.GetProperty("participants").EnumerateArray().ToArray();
        await Assert.That(after.GroupStatus).IsEqualTo("Open").Because(after.ToString());
        await Assert.That(after.OrderStatusCode).IsEqualTo(before.OrderStatusCode).Because(after.OrderBody);
        await Assert.That(JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(before.OrderBody),
            JsonSerializer.Deserialize<JsonElement>(after.OrderBody))).IsTrue().Because(after.OrderBody);
        await Assert.That(remaining.Length).IsEqualTo(participants.Length - 1).Because(after.Participants);
        await Assert.That(remaining.Any(item => item.GetProperty("id").GetString() == selection.SessionId)).IsFalse().Because(after.Participants);
        foreach (var item in remaining)
        {
            var original = participants.Single(candidate => candidate.GetProperty("id").GetString() == item.GetProperty("id").GetString());
            await Assert.That(JsonElement.DeepEquals(item, original)).IsTrue().Because(item.ToString());
        }
        await Assert.That(snapshot.GetProperty("total").GetDecimal()).IsEqualTo(
            beforeSnapshot.GetProperty("total").GetDecimal() - removed.GetProperty("total").GetDecimal()).Because(after.Participants);
        await CheckDisplayedSnapshotAsync(snapshot);
        var quantity = removed.GetProperty("isSubmitted").GetBoolean() ? 1 : 3;
        var screen = await Actor.AsksForAsync(new TheGroupSubmissionScreen());
        await Assert.That(screen.Rows.Count).IsEqualTo(1).Because(screen.ToString());
        await Assert.That(screen.Rows[0]).Contains(Recall<OwnedGroupData>().Dish.Name + " × " + quantity);
        await Assert.That(screen.Rows[0]).Contains((Recall<OwnedGroupData>().Dish.Price * quantity).ToString("C", CultureInfo.GetCultureInfo("en-US")));
    }

    [Then("only the owner's cart remains")]
    public async Task ThenOnlyOwner()
    {
        await Assert.That(await Actor.AsksForAsync(new TheGroupCardCount())).IsEqualTo(1);
        var cart = await Actor.AsksForAsync(new TheParticipantCart("My Pre-Order"));
        await Assert.That(cart.Dishes.Count).IsEqualTo(1).Because(cart.Text);
        await Assert.That(cart.Dishes[0].Quantity).IsEqualTo(1).Because(cart.Text);
    }

    [Then("the guest removal leaves an empty and open group")]
    public async Task ThenEmptyGroup()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(200).Because(outcome.Body);
        var returned = JsonSerializer.Deserialize<JsonElement>(outcome.Body);
        await Assert.That(returned.GetProperty("participants").GetArrayLength()).IsEqualTo(0).Because(outcome.Body);
        await Assert.That(returned.GetProperty("total").GetDecimal()).IsEqualTo(0m).Because(outcome.Body);
        var after = await Actor.AsksForAsync(new TheGroupSubmissionState());
        var snapshot = JsonSerializer.Deserialize<JsonElement>(after.Participants);
        await Assert.That(after.GroupStatus).IsEqualTo("Open").Because(after.ToString());
        await Assert.That(snapshot.GetProperty("participants").GetArrayLength()).IsEqualTo(0).Because(after.Participants);
        await Assert.That(snapshot.GetProperty("total").GetDecimal()).IsEqualTo(0m).Because(after.Participants);
        var before = Recall<GroupSubmissionState>("before-submit");
        await Assert.That(after.OrderStatusCode).IsEqualTo(before.OrderStatusCode).Because(after.OrderBody);
        await Assert.That(after.OrderBody).IsEqualTo(before.OrderBody).Because(after.OrderBody);
    }

    [Then("the selected guest and all displayed amounts remain")]
    public async Task ThenCancelled()
    {
        await Assert.That(Recall<string>("removal-dialog")).Contains(Recall<RemovalSelection>().Name);
        var before = Recall<GroupSubmissionState>("before-submit");
        await CheckDisplayedSnapshotAsync(JsonSerializer.Deserialize<JsonElement>(before.Participants));
        var screen = await Actor.AsksForAsync(new TheGroupSubmissionScreen());
        await Assert.That(screen.Rows.Count).IsEqualTo(1).Because(screen.ToString());
        await Assert.That(screen.Rows[0]).Contains(Recall<OwnedGroupData>().Dish.Name + " × 3");
        await Assert.That(screen.Rows[0]).Contains((Recall<OwnedGroupData>().Dish.Price * 3).ToString("C", CultureInfo.GetCultureInfo("en-US")));
    }

    [Then("the other group and order remain unchanged")]
    public async Task ThenOtherUnchanged() => await GroupSubmissionStepDefinitions.CheckUnchangedAsync(
        Recall<GroupSubmissionState>("other-group-before"), await Actor.AsksForAsync(new TheGroupSubmissionState("other-group")));

    [Then("guest removal is unavailable for the locked group")]
    public async Task ThenLocked()
    {
        var expected = Recall<string>("locked-group-status");
        var state = await Actor.AsksForAsync(new TheGroupSubmissionState());
        await Assert.That(state.GroupStatus).IsEqualTo(expected).Because(state.ToString());
        var screen = await Actor.AsksForAsync(new TheGroupSubmissionScreen());
        await Assert.That(screen.RemoveControls).IsEqualTo(0).Because(screen.ToString());
        await Assert.That(screen.EditControls).IsEqualTo(0).Because(screen.ToString());
        await Assert.That(screen.Badge.Trim()).IsEqualTo(expected == "Submitted" ? "Sent to Restaurant" : "Closed").Because(screen.ToString());
    }

    private async Task CheckDisplayedSnapshotAsync(JsonElement snapshot)
    {
        var participants = snapshot.GetProperty("participants").EnumerateArray().ToArray();
        await Assert.That(await Actor.AsksForAsync(new TheGroupCardCount())).IsEqualTo(participants.Length).Because(snapshot.ToString());
        foreach (var participant in participants)
        {
            var heading = participant.GetProperty("type").GetString() == "Owner" ? "My Pre-Order" : "Guest: " + participant.GetProperty("name").GetString();
            var cart = await Actor.AsksForAsync(new TheParticipantCart(heading));
            await Assert.That(cart.Subtotal).IsEqualTo(participant.GetProperty("total").GetDecimal()).Because(cart.Text);
            await Assert.That(cart.Submitted).IsEqualTo(participant.GetProperty("isSubmitted").GetBoolean()).Because(cart.Text);
            var items = participant.GetProperty("items").EnumerateArray().ToArray();
            await Assert.That(cart.Dishes.Count).IsEqualTo(items.Length).Because(cart.Text);
            foreach (var item in items)
            {
                var name = item.GetProperty("dish").GetProperty("name").GetString();
                var dish = cart.Dishes.Single(dish => dish.Name == name);
                var quantity = item.GetProperty("quantity").GetInt32();
                await Assert.That(dish.Quantity).IsEqualTo(quantity).Because(cart.Text);
                await Assert.That(dish.Total).IsEqualTo(item.GetProperty("dish").GetProperty("price").GetDecimal() * quantity).Because(cart.Text);
            }
        }
        await Assert.That(await Actor.AsksForAsync(new TheGroupGrandTotal())).IsEqualTo(
            participants.Where(item => item.GetProperty("isSubmitted").GetBoolean()).Sum(item => item.GetProperty("total").GetDecimal())).Because(snapshot.ToString());
    }
}
