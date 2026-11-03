using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.Components;

public sealed class GroupParticipantCard(ILocator root)
{
    public ILocator Root { get; } = root;
    public ILocator Status => Root.Locator(".status");
    public ILocator Subtotal => Root.Locator(".subtotal-value");
    public ILocator EditableQuantities => Root.Locator(".dish-controls span");
    public ILocator DishNames => Root.Locator(".dish-name");
    public ILocator Quantities => Root.Locator(".dish-controls span, .dish-qty");
    public ILocator LineTotals => Root.Locator(".dish-price");
}
