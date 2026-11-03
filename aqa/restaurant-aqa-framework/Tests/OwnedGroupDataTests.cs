using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;

namespace Restaurant_Aqa_Framework.Tests;

public sealed class OwnedGroupDataTests
{
    [Test]
    [Category("fixture")]
    public async Task PartiallyPreparedCustomerCanBeRemoved()
    {
        await using var cleanup = new OwnedGroupCleanup();
        await cleanup.CreateCustomerAsync(SampleDataFactory.ValidPassword());
        await cleanup.CleanUpAsync();
        await Assert.That(await cleanup.IsRemovedAsync()).IsTrue();
    }

    [Test]
    [Category("fixture")]
    public async Task PreparedGroupCanBeRemovedWithoutRemovingAnotherGroup()
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var api = await playwright.APIRequest.NewContextAsync(new() { BaseURL = Settings.Current.ApiBaseUrl });
        await using var first = await OwnedGroupData.CreateAsync(api);
        await using var second = await OwnedGroupData.CreateAsync(api);
        var firstSnapshot = await first.GetSnapshotAsync();
        await Assert.That(firstSnapshot.GetProperty("participants").GetArrayLength()).IsEqualTo(3);
        await Assert.That(firstSnapshot.GetProperty("total").GetDecimal()).IsEqualTo(first.Dish.Price * 4);
        await Assert.That(first.Reservation.Id).IsNotEqualTo(second.Reservation.Id);
        await Assert.That(first.Reservation.TableId).IsNotEqualTo(second.Reservation.TableId);
        await Assert.That(first.Reservation.TableId).IsNotEqualTo(Settings.Current.TestTableId);
        await first.Cleanup.CleanUpAsync();
        await Assert.That(await first.Cleanup.IsRemovedAsync()).IsTrue();
        var secondSnapshot = await second.GetSnapshotAsync();
        await Assert.That(secondSnapshot.GetProperty("participants").GetArrayLength()).IsEqualTo(3);
        await Assert.That(secondSnapshot.GetProperty("total").GetDecimal()).IsEqualTo(second.Dish.Price * 4);
        await second.Cleanup.CleanUpAsync();
        await Assert.That(await second.Cleanup.IsRemovedAsync()).IsTrue();
    }
}
