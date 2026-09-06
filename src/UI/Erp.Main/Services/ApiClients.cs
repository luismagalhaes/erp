namespace Erp.Main.Services;

public class CoreApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;
}

public class SalesApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;
}

public class InventoryApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;
}

public class PurchasingApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;
}

public class AccountingApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;
}

public class ReportingApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;
}
