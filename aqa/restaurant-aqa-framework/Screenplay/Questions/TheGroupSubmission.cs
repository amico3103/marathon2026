using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Ui;
using Restaurant_Aqa_Framework.Screenplay.Abilities;
using System.Text.Json;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed record GroupSubmissionState(string GroupStatus, int OrderStatusCode, string OrderBody, string Participants);
public sealed record SubmissionScreen(bool Disabled, string Badge, string Message, int EditControls, int RemoveControls, IReadOnlyList<string> Rows);

public sealed class TheGroupSubmissionState(string? fixtureName = null) : IQuestion<GroupSubmissionState>
{
    public async Task<GroupSubmissionState> AnsweredByAsync(Actor actor)
    {
        var data = actor.Recall<OwnedGroupData>(fixtureName);
        var api = actor.Recall<IAPIRequestContext>("fixture-api");
        var status = await new GroupOrdersApi(api).GetStatusAsync(data.GroupId);
        if (!status.Ok) throw new InvalidOperationException("Cannot read the group submission status.");
        var group = await status.ReadAsAsync<JsonElement>();
        var order = await new OrdersApi(api).GetOrderAsync(data.Reservation.Id, data.OwnerToken);
        return new(group.GetProperty("status").GetString()!, order.Status, await order.TextAsync(),
            (await data.GetSnapshotAsync()).GetRawText());
    }
}

public sealed class TheGroupSubmissionScreen : IQuestion<SubmissionScreen>
{
    public async Task<SubmissionScreen> AnsweredByAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        return new(await page.Submit.IsDisabledAsync(), await page.Badge.InnerTextAsync(),
            await page.SubmittedMessage.CountAsync() == 0 ? "" : await page.SubmittedMessage.InnerTextAsync(),
            await page.EditControls.CountAsync(), await page.RemoveGuest.CountAsync(), await page.ConsolidatedRows.AllInnerTextsAsync());
    }
}

public sealed class TheOwnerSession : IQuestion<JsonElement>
{
    public async Task<JsonElement> AnsweredByAsync(Actor actor)
    {
        var data = actor.Recall<OwnedGroupData>();
        var response = await new GroupOrdersApi(actor.Recall<IAPIRequestContext>("fixture-api"))
            .GetOwnerSessionAsync(data.Reservation.Id, data.OwnerToken);
        if (!response.Ok) throw new InvalidOperationException("Cannot read the owner session.");
        return await response.ReadAsAsync<JsonElement>();
    }
}

public sealed class TheWaiterGroupDishes : IQuestion<IReadOnlyList<(string Name, string Quantity)>>
{
    public Task<IReadOnlyList<(string Name, string Quantity)>> AnsweredByAsync(Actor actor) =>
        new WaiterPreOrderPage(actor.Recall<BrowseTheWeb>("waiter").Page).ReadDishesAsync();
}
