using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class GroupOrderPage(IPage page) : BasePage(page)
{
    public ILocator Cards => Page.Locator("app-participant-card");
    public ILocator GrandTotal => Page.Locator(".grand-total-value");
    public ILocator Notice => Page.Locator(".notice");
    public ILocator Error => Page.Locator("main [role='alert']");
    public ILocator Submit => Page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true });
    public ILocator SubmitDialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Submit Pre-Order?", Exact = true });
    public ILocator Badge => Page.Locator(".heading-row .badge");
    public ILocator SubmittedMessage => Page.Locator(".submitted-message");
    public ILocator EditControls => Page.Locator("app-participant-card .dish-controls button");
    public ILocator RemoveGuest => Page.Locator("app-participant-card .remove-button");
    public ILocator RemoveDialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Remove Guest", Exact = true });
    public ILocator ConsolidatedRows => Page.Locator(".consolidated li");

    public async Task<string> RemoveGuestAsync(string guestName, bool cancel)
    {
        var card = Card("Guest: " + guestName);
        await card.GetByRole(AriaRole.Button, new() { Name = "Remove " + guestName, Exact = true }).ClickAsync();
        await RemoveDialog.WaitForAsync();
        var message = await RemoveDialog.InnerTextAsync();
        await RemoveDialog.GetByRole(AriaRole.Button, new() { Name = cancel ? "Cancel" : "Remove", Exact = true }).ClickAsync();
        await RemoveDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        if (!cancel) await card.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        return message;
    }

    public async Task SubmitAsync()
    {
        await Submit.ClickAsync();
        await SubmitDialog.WaitForAsync();
        await SubmitDialog.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).ClickAsync();
        await SubmittedMessage.WaitForAsync();
    }

    public ILocator Card(string heading) => Cards.Filter(new()
    {
        Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }),
    });

    public async Task OpenAsync(string reservationId)
    {
        await NavigateToAsync($"/reservations/{reservationId}/group-order");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "My Pre-Order", Exact = true }).WaitForAsync();
    }

    public Task OpenMissingAsync(string reservationId) => NavigateToAsync($"/reservations/{reservationId}/group-order");
}
