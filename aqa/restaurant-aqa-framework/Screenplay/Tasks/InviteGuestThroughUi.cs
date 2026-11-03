using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Core;
using Restaurant_Aqa_Framework.Hooks;
using Restaurant_Aqa_Framework.Screenplay.Abilities;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class InviteGuestThroughUi(bool bookThroughUi = false, string? guestName = null, string? dishName = null, string guestContext = "guest") : ITask
{
    public const string VisitorName = "AQA-Sample-Visitor";

    public async Task PerformAsAsync(Actor actor)
    {
        var data = actor.Recall<OwnedGroupData>();
        var invitation = Settings.Current.UiBaseUrl.TrimEnd('/') + "/join/" + data.InviteToken;
        if (bookThroughUi)
        {
            await actor.AttemptsToAsync(new SignInThroughTheLoginForm(data.OwnerEmail, data.OwnerPassword));
            var booking = new GroupBookingPage(actor.LookingAtThePage());
            var reservation = await booking.BookAsync(data.LocationAddress, data.Table.TableNumber, data.BookingStart);
            await data.AdoptReservationAsync(reservation);
            invitation = await booking.ReadInvitationAsync();
            if (new Uri(invitation).Host != new Uri(Settings.Current.UiBaseUrl).Host ||
                new Uri(invitation).AbsolutePath != "/join/" + data.InviteToken)
                throw new InvalidOperationException("The displayed invitation does not belong to the prepared reservation.");
        }
        var context = await PlaywrightRunHooks.Browser.NewContextAsync(new() { BaseURL = Settings.Current.UiBaseUrl });
        context.SetDefaultTimeout(Settings.Current.DefaultTimeoutMs);
        context.SetDefaultNavigationTimeout(Settings.Current.NavigationTimeoutMs);
        actor.Recall<CleanupScope>().TrackAction(async () =>
        {
            try
            {
                await context.Tracing.StopAsync(new() { Path = Path.Combine(ArtifactPaths.Traces, $"guest-{Guid.NewGuid():N}.zip") });
            }
            finally { await context.CloseAsync(); }
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var guestPage = await context.NewPageAsync();
        actor.Remember(guestContext, new BrowseTheWeb(context, guestPage));
        var menu = new GuestGroupMenuPage(guestPage);
        await menu.JoinAsync(invitation, guestName ?? VisitorName);
        await menu.AddDishAsync(dishName ?? data.Dish.Name);
        if (bookThroughUi) await new GroupOrderPage(actor.LookingAtThePage()).OpenAsync(data.Reservation.Id);
    }
}
