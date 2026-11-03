using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Hooks;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class PrepareGroupReview(GroupDataShape shape) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var context = await PlaywrightRunHooks.Playwright.APIRequest.NewContextAsync(new()
        {
            BaseURL = Settings.Current.ApiBaseUrl,
            Timeout = Settings.Current.DefaultTimeoutMs,
        });
        var cleanup = actor.Recall<CleanupScope>();
        cleanup.TrackResource(context);
        actor.Remember("fixture-api", context);
        var data = await OwnedGroupData.CreateAsync(context, shape);
        cleanup.TrackResource(data);
        actor.Remember(data);
    }
}
