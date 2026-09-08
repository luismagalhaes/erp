using System.Net.Http.Json;
using Erp.Common;
using Erp.Main.Models.Core;
using Erp.Main.Models.Inventory;
using Erp.Main.Models.Notification;
using Erp.Main.Models.Purchasing;
using Erp.Main.Models.Sales;
using Erp.Main.Models.SeriesRegistry;

namespace Erp.Main.Services;

/// <summary>Shared helpers for reading API failures without leaking HTTP details into the UI.</summary>
public static class ApiResponse
{
    public static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return "Sem permissões para esta operação. Se as suas permissões mudaram há pouco, "
                 + "termine a sessão e volte a entrar: o token em cache ainda é o anterior.";
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
            if (!string.IsNullOrWhiteSpace(problem?.Error))
                return problem.Error;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            // Falls through to the status code message below.
        }

        return $"O pedido falhou com o estado {(int)response.StatusCode}.";
    }

    private sealed record ApiError(string? Error);
}
