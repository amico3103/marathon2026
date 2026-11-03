using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;
using System.Globalization;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class WaiterPreOrderPage(IPage page) : BasePage(page)
{
    public ILocator DishCards => Page.Locator(".preorder-list app-dish-card");

    public async Task OpenReservationAsync(DateTime date, int tableNumber)
    {
        await NavigateToAsync("/waiter-reservations");
        await new CustomSelectComponent(Page.Locator(".filtration app-custom-select").Nth(0))
            .SelectAsync(date.ToString("MMM d, yyyy", CultureInfo.GetCultureInfo("en-US")));
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply reservation filters", Exact = true }).ClickAsync();
        var card = Page.Locator("app-card-reservation").Filter(new()
        {
            Has = Page.Locator(".table-select").GetByText("Table " + tableNumber.ToString(CultureInfo.InvariantCulture), new() { Exact = true }),
        });
        await card.GetByRole(AriaRole.Button, new() { Name = "Pre-order", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync("**/waiter-pre-order");
        await DishCards.First.WaitForAsync();
    }

    public async Task<IReadOnlyList<(string Name, string Quantity)>> ReadDishesAsync()
    {
        var result = new List<(string Name, string Quantity)>();
        foreach (var card in await DishCards.AllAsync())
            result.Add((await card.Locator(".dish-name").InnerTextAsync(), await card.Locator(".pre-order-quantity").InnerTextAsync()));
        return result;
    }
}
