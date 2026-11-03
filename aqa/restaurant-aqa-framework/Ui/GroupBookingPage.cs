using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using System.Globalization;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class GroupBookingPage(IPage page)
{
    public async Task<ReservationResponse> BookAsync(string location, int tableNumber, DateTime date)
    {
        var tables = new TablesPage(page);
        await tables.NavigateAsync();
        await tables.LocationFilter.SelectAsync(location);
        await tables.DateFilter.SelectAsync(date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture));
        await tables.TimeFilter.SelectAsync("17:00");
        await tables.GuestsFilter.SetAsync(3);
        await tables.FindTablesAsync();
        await tables.TableByNumber(tableNumber).SelectAvailableTimeSlotAsync();
        var response = await page.RunAndWaitForResponseAsync(
            () => page.GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true }).ClickAsync(),
            response => response.Request.Method == "POST" && new Uri(response.Url).AbsolutePath.EndsWith("/reservations", StringComparison.Ordinal));
        if (!response.Ok) throw new InvalidOperationException($"Booking through the UI failed: HTTP {response.Status}.");
        var reservation = System.Text.Json.JsonSerializer.Deserialize<ReservationResponse>(await response.TextAsync(), JsonDefaults.Options)
            ?? throw new InvalidOperationException("Booking returned no reservation.");
        await page.GetByLabel("Invite Link", new() { Exact = true }).WaitForAsync();
        return reservation;
    }

    public async Task<string> ReadInvitationAsync()
    {
        var input = page.GetByLabel("Invite Link", new() { Exact = true });
        await input.WaitForAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('#invite-link-input')?.value.includes('/join/')");
        return await input.InputValueAsync();
    }
}
