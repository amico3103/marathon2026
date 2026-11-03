using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Ui;
using Restaurant_Aqa_Framework.Ui.Components;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed record ParticipantDishView(string Name, int Quantity, decimal Total);
public sealed record ParticipantCartView(bool Submitted, decimal Subtotal, IReadOnlyList<ParticipantDishView> Dishes, string Text);

public sealed class TheParticipantCart(string heading, int? waitForQuantity = null, bool? waitForSubmission = null) : IQuestion<ParticipantCartView>
{
    public async Task<ParticipantCartView> AnsweredByAsync(Actor actor)
    {
        var card = new GroupParticipantCard(new GroupOrderPage(actor.LookingAtThePage()).Card(heading));
        await card.Root.WaitForAsync();
        if (waitForQuantity is not null)
            await card.EditableQuantities.Filter(new()
            {
                HasTextRegex = new Regex("^\\s*" + waitForQuantity.Value.ToString(CultureInfo.InvariantCulture) + "\\s*$"),
            }).WaitForAsync(new() { Timeout = 45000 });
        if (waitForSubmission is not null)
            await card.Status.Filter(new() { HasText = waitForSubmission.Value ? "Submitted" : "Still choosing" })
                .WaitForAsync(new() { Timeout = 45000 });
        var names = await card.DishNames.AllInnerTextsAsync();
        var quantities = await card.Quantities.AllInnerTextsAsync();
        var prices = await card.LineTotals.AllInnerTextsAsync();
        var dishes = new List<ParticipantDishView>();
        for (var index = 0; index < names.Count; index++)
        {
            dishes.Add(new(names[index], int.Parse(Regex.Match(quantities[index], "[0-9]+").Value, CultureInfo.InvariantCulture), Money(prices[index])));
        }
        return new((await card.Status.InnerTextAsync()).Contains("Submitted", StringComparison.Ordinal),
            Money(await card.Subtotal.InnerTextAsync()), dishes, await card.Root.InnerTextAsync());
    }

    internal static decimal Money(string text) => decimal.Parse(text.Trim(), NumberStyles.Currency, CultureInfo.GetCultureInfo("en-US"));
}

public sealed class TheGroupGrandTotal : IQuestion<decimal>
{
    public async Task<decimal> AnsweredByAsync(Actor actor) =>
        TheParticipantCart.Money(await new GroupOrderPage(actor.LookingAtThePage()).GrandTotal.InnerTextAsync());
}

public sealed class TheGroupCardCount : IQuestion<int>
{
    public Task<int> AnsweredByAsync(Actor actor) => new GroupOrderPage(actor.LookingAtThePage()).Cards.CountAsync();
}

public sealed class TheGroupNotice : IQuestion<string>
{
    public Task<string> AnsweredByAsync(Actor actor) => new GroupOrderPage(actor.LookingAtThePage()).Notice.InnerTextAsync();
}

public sealed class TheApiGroupSnapshot : IQuestion<JsonElement>
{
    public Task<JsonElement> AnsweredByAsync(Actor actor) => actor.Recall<OwnedGroupData>().GetSnapshotAsync();
}

public sealed class TheMissingGroupMessage : IQuestion<string>
{
    public async Task<string> AnsweredByAsync(Actor actor)
    {
        var error = new GroupOrderPage(actor.LookingAtThePage()).Error;
        await error.WaitForAsync();
        return await error.InnerTextAsync();
    }
}
