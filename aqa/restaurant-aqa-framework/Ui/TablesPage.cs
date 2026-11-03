using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class TablesPage(IPage page) : BasePage(page)
{
    public HeaderComponent Header { get; } = new(page);
    public MakeReservationModalComponent MakeReservationModal { get; } = new(page);
    public ReservationConfirmedModalComponent ReservationConfirmed { get; } = new(page);

    private ILocator GenericFilters => Page.Locator(".booking-card app-custom-select");
    public CustomSelectComponent LocationFilter => new(GenericFilters.Nth(0));
    public CustomSelectComponent DateFilter => new(GenericFilters.Nth(1));
    public CustomSelectComponent TimeFilter => new(GenericFilters.Nth(2));
    public CustomNumberComponent GuestsFilter => new(Page.Locator(".booking-card .field-guests app-custom-number"));
    public ILocator FindTableButton => Page.GetByRole(AriaRole.Button, new() { Name = "Find a Table", Exact = true });

    private ILocator TableCards => Page.Locator(".table-card-list > app-table-card");

    public Task NavigateAsync() => NavigateToAsync("/tables");

    public Task NavigateWithLocationFilterAsync(string locationId) => NavigateToAsync($"/tables?locationId={locationId}");

    public Task FindTablesAsync() => FindTableButton.ClickAsync();

    public TableCardComponent Table(int index) => new(TableCards.Nth(index));

    public TableCardComponent TableByNumber(int tableNumber) =>
        new(TableCards.Filter(new() { Has = Page.GetByText($"Table {tableNumber}", new() { Exact = true }) }));
}
