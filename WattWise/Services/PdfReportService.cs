using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WattWise.ViewModels;

namespace WattWise.Services;

/// <summary>
/// Generates a downloadable monthly electricity report PDF using QuestPDF.
/// All values come from actual database records — nothing is fabricated.
/// </summary>
public class PdfReportService
{
    // ── Colour palette ────────────────────────────────────────────────────────
    private static readonly string AccentAmber = "#F59E0B";
    private static readonly string Dark        = "#1E293B";
    private static readonly string Mid         = "#334155";
    private static readonly string Light       = "#94A3B8";
    private static readonly string White       = "#FFFFFF";
    private static readonly string SolarGreen  = "#22C55E";

    public byte[] Generate(MonthlyReportData data)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(ts => ts.FontSize(10).FontFamily("Arial"));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    // ── Header ────────────────────────────────────────────────────────────────
    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("⚡ WattWise")
                        .Bold().FontSize(22).FontColor(AccentAmber);
                    c.Item().Text("Smart Electricity Usage Tracker")
                        .FontSize(10).FontColor(Light);
                });
                row.ConstantItem(120).AlignRight().Column(c =>
                {
                    c.Item().Text("MONTHLY REPORT")
                        .Bold().FontSize(13).FontColor(White);
                    c.Item().Text("Estimate — Not an Official Bill")
                        .FontSize(8).FontColor(AccentAmber).Italic();
                });
            });

            col.Item().PaddingTop(4).LineHorizontal(2).LineColor(AccentAmber);
        });
    }

    // ── Footer ────────────────────────────────────────────────────────────────
    private void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Mid);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("⚠ IMPORTANT: ").Bold().FontColor(AccentAmber).FontSize(8);
                    t.Span("All charges shown are estimates based on configured tariff slabs. " +
                           "Actual bills may include additional taxes, fixed charges, and surcharges " +
                           "from your electricity distribution company. Always refer to your official bill.")
                        .FontSize(8).FontColor(Light);
                });
                row.ConstantItem(80).AlignRight().Text(t =>
                    t.CurrentPageNumber().FontSize(8).FontColor(Light));
            });
        });
    }

    // ── Main content ──────────────────────────────────────────────────────────
    private void ComposeContent(IContainer container, MonthlyReportData data)
    {
        container.Column(col =>
        {
            col.Spacing(12);

            // ── Report meta row ───────────────────────────────────────────────
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => InfoBox(c, "Report Period", data.MonthLabel, AccentAmber));
                row.ConstantItem(8);
                row.RelativeItem().Element(c => InfoBox(c, "Generated", data.GeneratedAt.ToString("dd MMM yyyy, HH:mm"), Light));
                row.ConstantItem(8);
                row.RelativeItem().Element(c => InfoBox(c, "Account", data.UserName, Light));
            });

            // ── Connection details ────────────────────────────────────────────
            col.Item().Element(c => SectionHeader(c, "Connection Details"));
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                });

                TableRow(table, "Consumer Number", data.ConsumerNumber,
                                "Connection Type",  data.ConnectionType);
                TableRow(table, "Address", data.Address,
                                "Sanctioned Load",  data.SanctionedLoad + " kW");
                TableRow(table, "Solar Installation",
                                data.HasSolar ? (data.SolarCapacityKw.HasValue
                                    ? $"Yes — {data.SolarCapacityKw.Value:F2} kWp" : "Yes") : "No",
                                "Battery Storage",
                                data.HasSolar ? (data.HasBattery ? "Yes" : "No") : "—");
            });

            // ── Grid meter readings ───────────────────────────────────────────
            col.Item().Element(c => SectionHeader(c, "Grid Import Meter Readings"));

            if (!data.HasReadingData)
            {
                col.Item().Background(Mid).Padding(10)
                    .Text("No meter reading found for this connection in the selected month.")
                    .FontColor(Light).Italic();
            }
            else
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(3);
                    });

                    TableRow(table, "Reading Date",
                                    data.ReadingDate?.ToString("dd MMM yyyy") ?? "—",
                                    "Payment Status",
                                    data.IsPaid ? "✓ Paid" : "⏳ Pending");
                    TableRow(table, "Previous Reading",
                                    data.PreviousGridReading.HasValue ? $"{data.PreviousGridReading:F2} kWh" : "—",
                                    "Current Reading",
                                    data.CurrentGridReading.HasValue ? $"{data.CurrentGridReading:F2} kWh" : "—");

                    // Grid import highlight row
                    table.Cell().ColumnSpan(2).Background(Dark).Padding(6)
                        .Text("Grid Consumption (Import)").Bold().FontColor(Light).FontSize(9);
                    table.Cell().ColumnSpan(2).Background(Dark).Padding(6)
                        .Text(data.GridImportKwh.HasValue ? $"{data.GridImportKwh:F2} kWh" : "Data unavailable")
                        .Bold().FontColor(AccentAmber).FontSize(11);
                });

                // ── Bill estimate ─────────────────────────────────────────────
                col.Item().Element(c => SectionHeader(c, "Estimated Bill"));
                col.Item().Background(Dark).Padding(12).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Estimated Electricity Charge").FontColor(Light).FontSize(9);
                        c.Item().Text(data.BillAmount.HasValue
                                ? $"₹ {data.BillAmount.Value:F2}"
                                : "Data unavailable")
                            .Bold().FontSize(18).FontColor(AccentAmber);
                        c.Item().PaddingTop(4)
                            .Text("This is an estimate using tariff slabs below. Not an official bill.")
                            .FontSize(8).FontColor(Light).Italic();
                    });
                });

                // Tariff slabs used
                if (data.AppliedSlabs.Any())
                {
                    col.Item().Element(c => SectionHeader(c, "Tariff Slabs Applied"));
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(4);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        // Header
                        table.Cell().Background(Mid).Padding(5).Text("Slab").Bold().FontColor(White).FontSize(9);
                        table.Cell().Background(Mid).Padding(5).Text("Rate/Unit").Bold().FontColor(White).FontSize(9);
                        table.Cell().Background(Mid).Padding(5).Text("Fixed Charge").Bold().FontColor(White).FontSize(9);

                        foreach (var slab in data.AppliedSlabs)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
                                .Text(slab.SlabLabel).FontSize(9).FontColor(Light);
                            table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
                                .Text($"₹ {slab.RatePerUnit:F4}").FontSize(9).FontColor(Light);
                            table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
                                .Text($"₹ {slab.FixedCharge:F2}").FontSize(9).FontColor(Light);
                        }
                    });
                }
            }

            // ── Solar section (only when HasSolar=true) ───────────────────────
            if (data.HasSolar)
            {
                col.Item().Element(c => SectionHeader(c, "Solar Generation & Export", SolarGreen));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(3);
                    });

                    // Header
                    table.Cell().Background(Mid).Padding(5).Text("Metric").Bold().FontColor(White).FontSize(9);
                    table.Cell().Background(Mid).Padding(5).Text("Value").Bold().FontColor(White).FontSize(9);
                    table.Cell().Background(Mid).Padding(5).Text("Note").Bold().FontColor(White).FontSize(9);

                    SolarRow(table, "Solar Generation",
                        data.SolarGenerationKwh, " kWh",
                        "From generation meter readings");

                    SolarRow(table, "Grid Export",
                        data.GridExportKwh, " kWh",
                        "From export meter readings");

                    if (data.HasBattery)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
                            .Text("Solar Self-Consumed").FontSize(9).FontColor(Light);
                        table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
                            .Text("Data unavailable").FontSize(9).FontColor(AccentAmber).Italic();
                        table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
                            .Text("Battery present — Gen−Export formula not reliable").FontSize(8).FontColor(Light);
                    }
                    else
                    {
                        SolarRow(table, "Solar Self-Consumed",
                            data.SolarSelfConsumedKwh, " kWh",
                            "Generation − Export (no battery assumed)");
                    }
                });

                col.Item().Background(Dark).Padding(8)
                    .Text("⚠ Solar figures are calculated from cumulative meter readings and are approximate. " +
                          "Export credits, net-metering rules, and off-peak rates vary by utility — " +
                          "they are not included in the bill estimate above.")
                    .FontSize(8).FontColor(AccentAmber).Italic();
            }

            // ── Remarks ───────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(data.Remarks))
            {
                col.Item().Element(c => SectionHeader(c, "Remarks"));
                col.Item().Background(Dark).Padding(8).Text(data.Remarks).FontColor(Light).FontSize(9);
            }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static void SectionHeader(IContainer container, string title, string colour = "#22D3EE")
    {
        container.PaddingTop(4).Column(col =>
        {
            col.Item().Text(title).Bold().FontSize(11).FontColor(colour);
            col.Item().LineHorizontal(1).LineColor(Mid);
        });
    }

    private static void InfoBox(IContainer container, string label, string value, string colour)
    {
        container.Background(Dark).Padding(8).Column(c =>
        {
            c.Item().Text(label).FontSize(8).FontColor(Light);
            c.Item().Text(value).Bold().FontSize(11).FontColor(colour);
        });
    }

    private static void TableRow(TableDescriptor table,
        string k1, string v1, string k2, string v2)
    {
        table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
            .Text(k1).FontSize(9).FontColor(Light);
        table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
            .Text(v1).FontSize(9).Bold().FontColor(White);
        table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
            .Text(k2).FontSize(9).FontColor(Light);
        table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
            .Text(v2).FontSize(9).Bold().FontColor(White);
    }

    private static void SolarRow(TableDescriptor table, string metric,
        decimal? value, string unit, string note)
    {
        table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
            .Text(metric).FontSize(9).FontColor(Light);
        table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
            .Text(value.HasValue ? $"{value.Value:F2}{unit}" : "Data unavailable")
            .FontSize(9).Bold().FontColor(value.HasValue ? White : AccentAmber).Italic(!value.HasValue);
        table.Cell().BorderBottom(1).BorderColor(Mid).Padding(5)
            .Text(note).FontSize(8).FontColor(Light);
    }
}
