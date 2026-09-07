using Erp.Purchasing.Domain;
using FluentAssertions;

namespace Erp.Purchasing.Tests;

/// <summary>
/// The order itself. Unlike a sales document it is not append-only — nothing here was issued to
/// anyone — so most of these tests are about what may still change and what may not.
/// </summary>
public class PurchaseOrderTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private static readonly SupplierSnapshot Supplier =
        new("F001", "Fornecedor Teste, Lda", "501234567");

    private static PurchaseOrderLine Line(
        string productCode = "ART001",
        decimal quantity = 10m,
        decimal unitPrice = 5m,
        decimal taxPercentage = 23m) => new()
    {
        ProductCode = productCode,
        ProductDescription = "Artigo de teste",
        Quantity = quantity,
        UnitPrice = unitPrice,
        TaxPercentage = taxPercentage
    };

    private static PurchaseOrder Create(params PurchaseOrderLine[] lines) =>
        PurchaseOrder.Create(
            CompanyId,
            SupplierId,
            Supplier,
            "ENC2026/1",
            new DateOnly(2026, 3, 1),
            WarehouseId,
            lines.Length == 0 ? [Line()] : lines,
            new DateOnly(2026, 3, 15),
            createdByUserId: "user-1");

    private static PurchaseOrder Placed(params PurchaseOrderLine[] lines)
    {
        var order = Create(lines);
        order.Place();
        return order;
    }

    [Fact]
    public void Create_starts_as_a_draft()
    {
        var order = Create();

        order.Status.Should().Be(PurchaseOrderStatus.Draft);
        order.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Create_numbers_the_lines_in_order()
    {
        var order = Create(Line(), Line("ART002"), Line("ART003"));

        order.Lines.Select(line => line.LineNumber).Should().Equal(1, 2, 3);
        order.Lines.Should().OnlyContain(line => line.OrderId == order.Id);
    }

    [Fact]
    public void Create_computes_the_line_and_document_totals()
    {
        var order = Create(Line(quantity: 10m, unitPrice: 5m), Line("ART002", 2m, 10m));

        order.NetTotal.Should().Be(70m);
        order.TaxTotal.Should().Be(16.10m);
        order.GrossTotal.Should().Be(86.10m);
    }

    [Fact]
    public void Create_refuses_an_order_with_no_lines()
    {
        var act = () => PurchaseOrder.Create(
            CompanyId, SupplierId, Supplier, "ENC2026/1", new DateOnly(2026, 3, 1), WarehouseId, []);

        act.Should().Throw<ArgumentException>().WithMessage("*orders nothing*");
    }

    [Fact]
    public void Create_refuses_a_line_with_no_quantity()
    {
        var act = () => Create(Line(quantity: 0m));

        act.Should().Throw<ArgumentException>().WithMessage("*orders no quantity*");
    }

    [Fact]
    public void Create_refuses_an_order_with_no_warehouse()
    {
        var act = () => PurchaseOrder.Create(
            CompanyId, SupplierId, Supplier, "ENC2026/1", new DateOnly(2026, 3, 1), Guid.Empty, [Line()]);

        act.Should().Throw<ArgumentException>().WithMessage("*where the goods are to be received*");
    }

    [Fact]
    public void Place_sends_the_order_to_the_supplier()
    {
        var order = Placed();

        order.Status.Should().Be(PurchaseOrderStatus.Placed);
        order.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void Place_refuses_an_order_already_placed()
    {
        var order = Placed();

        var act = order.Place;

        act.Should().Throw<InvalidOperationException>().WithMessage("*already been placed*");
    }

    // --- Receiving ---

    [Fact]
    public void RegisterReceipt_moves_the_order_to_partially_received()
    {
        var order = Placed(Line(quantity: 10m));
        var line = order.Lines.Single();

        order.RegisterReceipt(line.Id, 4m);

        order.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        line.ReceivedQuantity.Should().Be(4m);
        line.PendingQuantity.Should().Be(6m);
    }

    [Fact]
    public void RegisterReceipt_closes_the_order_once_everything_arrived()
    {
        var order = Placed(Line(quantity: 10m));
        var line = order.Lines.Single();

        order.RegisterReceipt(line.Id, 10m);

        order.Status.Should().Be(PurchaseOrderStatus.Received);
        line.IsFullyReceived.Should().BeTrue();
        line.PendingQuantity.Should().Be(0m);
    }

    /// <summary>
    /// Suppliers over-deliver, and it is not the order's business to refuse it. What it must not do
    /// is turn the excess into a negative debt.
    /// </summary>
    [Fact]
    public void RegisterReceipt_accepts_more_than_was_ordered_without_owing_a_negative()
    {
        var order = Placed(Line(quantity: 10m));
        var line = order.Lines.Single();

        order.RegisterReceipt(line.Id, 12m);

        line.ReceivedQuantity.Should().Be(12m);
        line.PendingQuantity.Should().Be(0m);
        order.Status.Should().Be(PurchaseOrderStatus.Received);
    }

    [Fact]
    public void RegisterReceipt_refuses_a_draft()
    {
        var order = Create();

        var act = () => order.RegisterReceipt(order.Lines.Single().Id, 1m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not been placed*");
    }

    [Fact]
    public void RegisterReceipt_refuses_a_line_from_another_order()
    {
        var order = Placed();

        var act = () => order.RegisterReceipt(Guid.NewGuid(), 1m);

        act.Should().Throw<ArgumentException>().WithMessage("*does not belong*");
    }

    [Fact]
    public void ReverseReceipt_puts_the_order_back_where_it_was()
    {
        var order = Placed(Line(quantity: 10m));
        var line = order.Lines.Single();
        order.RegisterReceipt(line.Id, 10m);

        order.ReverseReceipt(line.Id, 10m);

        line.ReceivedQuantity.Should().Be(0m);
        order.Status.Should().Be(PurchaseOrderStatus.Placed);
        order.HasReceipts.Should().BeFalse();
    }

    [Fact]
    public void ReverseReceipt_leaves_a_partly_received_order_partly_received()
    {
        var order = Placed(Line(quantity: 10m));
        var line = order.Lines.Single();
        order.RegisterReceipt(line.Id, 10m);

        order.ReverseReceipt(line.Id, 4m);

        line.ReceivedQuantity.Should().Be(6m);
        order.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
    }

    // --- What may still change ---

    [Fact]
    public void ReplaceLines_rewrites_an_order_that_has_received_nothing()
    {
        var order = Placed(Line(quantity: 10m));

        order.ReplaceLines([Line("ART002", 3m, 20m)]);

        order.Lines.Should().ContainSingle().Which.ProductCode.Should().Be("ART002");
        order.NetTotal.Should().Be(60m);
    }

    /// <summary>
    /// Changing a line after goods arrived would silently rewrite what the receipt was measured
    /// against — the received quantity would suddenly refer to a different line.
    /// </summary>
    [Fact]
    public void ReplaceLines_refuses_an_order_that_already_received_goods()
    {
        var order = Placed(Line(quantity: 10m));
        order.RegisterReceipt(order.Lines.Single().Id, 1m);

        var act = () => order.ReplaceLines([Line("ART002")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*goods received*");
    }

    [Fact]
    public void UpdateHeader_changes_the_dates_and_the_warehouse()
    {
        var order = Placed();
        var warehouse = Guid.NewGuid();

        order.UpdateHeader(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 20), warehouse, "  urgente  ");

        order.OrderDate.Should().Be(new DateOnly(2026, 4, 1));
        order.ExpectedDate.Should().Be(new DateOnly(2026, 4, 20));
        order.WarehouseId.Should().Be(warehouse);
        order.Notes.Should().Be("urgente");
    }

    // --- Finishing ---

    [Fact]
    public void Close_writes_off_what_is_still_owed()
    {
        var order = Placed(Line(quantity: 10m));
        order.RegisterReceipt(order.Lines.Single().Id, 4m);
        var closedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

        order.Close("O fornecedor não entrega o resto", closedAt);

        order.Status.Should().Be(PurchaseOrderStatus.Closed);
        order.ClosedAtUtc.Should().Be(closedAt);
        order.ClosedReason.Should().Be("O fornecedor não entrega o resto");
        order.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Close_refuses_a_draft()
    {
        var order = Create();

        var act = () => order.Close("Enganei-me", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cancelled, not closed*");
    }

    [Fact]
    public void Cancel_calls_off_an_order_that_received_nothing()
    {
        var order = Placed();

        order.Cancel("Já não é preciso", DateTime.UtcNow);

        order.Status.Should().Be(PurchaseOrderStatus.Cancelled);
    }

    /// <summary>Goods that arrived cannot be un-ordered; the rest is written off instead.</summary>
    [Fact]
    public void Cancel_refuses_an_order_that_received_goods()
    {
        var order = Placed(Line(quantity: 10m));
        order.RegisterReceipt(order.Lines.Single().Id, 1m);

        var act = () => order.Cancel("Já não é preciso", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*can only be closed*");
    }

    [Fact]
    public void A_finished_order_cannot_be_changed()
    {
        var order = Placed();
        order.Cancel("Já não é preciso", DateTime.UtcNow);

        var updateHeader = () => order.UpdateHeader(new DateOnly(2026, 4, 1), null, WarehouseId, null);
        var replaceLines = () => order.ReplaceLines([Line()]);
        var close = () => order.Close("Outra razão", DateTime.UtcNow);

        updateHeader.Should().Throw<InvalidOperationException>().WithMessage("*finished*");
        replaceLines.Should().Throw<InvalidOperationException>().WithMessage("*finished*");
        close.Should().Throw<InvalidOperationException>().WithMessage("*finished*");
    }

    [Fact]
    public void RegisterReceipt_refuses_a_cancelled_order()
    {
        var order = Placed();
        var lineId = order.Lines.Single().Id;
        order.Cancel("Já não é preciso", DateTime.UtcNow);

        var act = () => order.RegisterReceipt(lineId, 1m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cancelled*");
    }
}
