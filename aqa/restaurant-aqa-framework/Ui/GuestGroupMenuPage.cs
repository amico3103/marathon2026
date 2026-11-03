using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class GuestGroupMenuPage(IPage page)
{
    public ILocator Cart => page.GetByRole(AriaRole.Dialog, new() { Name = "Cart", Exact = true });

    public async Task JoinAsync(string invitation, string name)
    {
        await page.GotoAsync(invitation);
        await page.GetByLabel("Enter Your Name", new() { Exact = true }).FillAsync(name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Browse Menu", Exact = true }).ClickAsync();
        await page.Locator("app-guest-name-modal").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await page.Locator("app-dish-card").First.WaitForAsync();
    }

    public async Task AddDishAsync(string name)
    {
        var card = page.Locator("app-dish-card").Filter(new() { Has = page.GetByText(name, new() { Exact = true }) });
        await new Components.DishCardComponent(card).PreOrderAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Open cart", Exact = true }).ClickAsync();
        await Cart.Locator(".qty-value").Filter(new() { HasText = "1" }).WaitForAsync();
    }

    public async Task IncreaseAsync()
    {
        await Cart.GetByRole(AriaRole.Button, new() { Name = "Increase quantity", Exact = true }).ClickAsync();
        await Cart.Locator(".qty-value").Filter(new() { HasText = "2" }).WaitForAsync();
    }

    public async Task SubmitAsync()
    {
        await Cart.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true }).ClickAsync();
        await page.Locator("app-guest-preorder-submitted").WaitForAsync();
    }
}
