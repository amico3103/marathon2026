using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>Business-level outcomes shared by several features ("the visitor is told ...", "could not be
/// found"). How an outcome shows up on the wire — which status code, which error body — is decided here,
/// so feature files never mention it.</summary>
[Binding]
public sealed class OutcomeStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Then("removing the guest is refused because {string}")]
    public async Task ThenGuestRemovalRefused(string reason)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        var (status, message) = reason switch
        {
            "the guest belongs to another group" => (400, "Guest session does not belong to this group reservation."),
            "the guest is missing" => (404, "not found"),
            "the owner must remain" => (400, "The owner session cannot be deleted."),
            "the group is locked" => (409, Recall<string>("locked-group-status") == "Submitted" ? "submitted" : "expired"),
            _ => throw new InvalidOperationException("Unsupported guest removal refusal."),
        };
        await Assert.That(outcome.Status).IsEqualTo(status).Because(outcome.Body);
        await Assert.That(outcome.ErrorMessage).Contains(message);
    }

    [Then("removing the guest is restricted to its owner")]
    public Task ThenGuestRemovalProtected() => ThenGroupSubmissionProtected();

    [Then("sending the group is refused because {string}")]
    public async Task ThenGroupSubmissionRefused(string reason)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        var (status, message) = reason switch
        {
            "no carts are confirmed" => (400, "No submitted guest sessions were found."),
            "confirmed carts contain no dishes" => (400, "Submitted guest sessions contain no dishes."),
            "the owner order is missing" => (400, "Order was not found for this reservation."),
            "the reservation is missing" => (404, "Reservation not found."),
            "the group is missing" => (404, "Group reservation"),
            "the group was already submitted" => (409, "submitted"),
            _ => throw new InvalidOperationException("Unsupported submission refusal."),
        };
        await Assert.That(outcome.Status).IsEqualTo(status).Because(outcome.Body);
        await Assert.That(outcome.ErrorMessage).Contains(message);
    }

    [Then("sending the group is restricted to its owner")]
    public async Task ThenGroupSubmissionProtected()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        var expected = Recall<ParticipantRequester>().Name is "guest" or "anonymous visitor" ? 401 : 403;
        await Assert.That(outcome.Status).IsEqualTo(expected).Because(outcome.Body);
        await Assert.That(outcome.Succeeded).IsFalse().Because(outcome.Body);
    }

    [Then("the participant information is protected")]
    public async Task ThenParticipantsProtected()
    {
        var requester = Recall<ParticipantRequester>().Name;
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        var expected = requester is "guest" or "anonymous visitor" ? 401 : 403;
        await Assert.That(outcome.Status).IsEqualTo(expected).Because($"{requester}: {outcome.Body}");
        await Assert.That(outcome.Body.Contains("\"participants\"", StringComparison.Ordinal)).IsFalse().Because(outcome.Body);
    }

    [Then("the missing group is reported")]
    public async Task ThenMissingGroupReported()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(404).Because(outcome.Body);
        await Assert.That(outcome.ErrorMessage).Contains("Group reservation");
    }

    [Then("the visitor is told {string}")]
    [Scope(Tag = "api")]
    public async Task ThenVisitorIsTold(string message)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());

        await Assert.That(outcome.Succeeded).IsFalse().Because($"the request unexpectedly succeeded: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).IsEqualTo(message);
    }

    [Then("sign-in is refused")]
    public async Task ThenSignInIsRefused()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(401).Because($"response body was: {outcome.Body}");
    }

    [Then("the guest is told that the {item} could not be found")]
    public async Task ThenGuestIsToldNotFound(string item)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());

        await Assert.That(outcome.Status).IsEqualTo(404).Because($"response body was: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains("was not found");
    }

    [Then("the location is not created")]
    public async Task ThenLocationIsNotCreated()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsGreaterThanOrEqualTo(400).Because($"the location was created: {outcome.Body}");
    }

    [Then("the information is available")]
    [Then("the menu is shown")]
    public async Task ThenInformationIsAvailable()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Succeeded).IsTrue().Because($"response status {outcome.Status}, body: {outcome.Body}");
    }
}
