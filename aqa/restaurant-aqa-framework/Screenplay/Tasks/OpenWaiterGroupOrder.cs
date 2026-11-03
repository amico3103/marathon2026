using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Core;
using Restaurant_Aqa_Framework.Hooks;
using Restaurant_Aqa_Framework.Screenplay.Abilities;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class OpenWaiterGroupOrder : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var data = actor.Recall<OwnedGroupData>();
        var context = await PlaywrightRunHooks.Browser.NewContextAsync(new() { BaseURL = Settings.Current.UiBaseUrl });
        context.SetDefaultTimeout(Settings.Current.DefaultTimeoutMs);
        context.SetDefaultNavigationTimeout(Settings.Current.NavigationTimeoutMs);
        var tracing = false;
        actor.Recall<CleanupScope>().TrackAction(async () =>
        {
            try
            {
                if (tracing) await context.Tracing.StopAsync(new() { Path = Path.Combine(ArtifactPaths.Traces, $"waiter-{Guid.NewGuid():N}.zip") });
            }
            finally { await context.CloseAsync(); }
        });
        var page = await context.NewPageAsync();
        actor.Remember("waiter", new BrowseTheWeb(context, page));
        var login = new LoginPage(page);
        await login.NavigateAsync();
        var credentials = Settings.Current.RequireWaiter();
        // Start recording after sign-in so the permanent waiter's password is never in the trace.
        await login.SignInAsync(credentials.Username, credentials.Password);
        await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.Ordinal));
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        tracing = true;
        await new WaiterPreOrderPage(page).OpenReservationAsync(data.BookingStart, data.Table.TableNumber);
    }
}
