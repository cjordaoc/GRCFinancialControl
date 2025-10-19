using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using App.Presentation.Localization;
using ClosedXML.Excel;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InvoicePlanner.Avalonia.Services;

public class InvoiceSummaryExporter
{
    public byte[] CreateExcel(InvoiceSummaryResult result)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet(LocalizationRegistry.Get("Exports.InvoiceSummary.WorksheetName"));

        var headers = new[]
        {
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Customer"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.CustomerCode"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Engagement"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.PlanId"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Line"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Status"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.PlanType"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Percentage"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Amount"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.BaseValue"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.EmissionDate"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.DueDate"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.RequestDate"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.EmittedAt"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.BzCode"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Ritm"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.CanceledAt"),
            LocalizationRegistry.Get("Exports.InvoiceSummary.Header.CancelReason"),
        };

        for (var index = 0; index < headers.Length; index++)
        {
            var cell = worksheet.Cell(1, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#00338D");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var rowNumber = 2;
        foreach (var group in result.Groups)
        {
            foreach (var item in group.Items)
            {
                worksheet.Cell(rowNumber, 1).Value = group.CustomerName ?? string.Empty;
                worksheet.Cell(rowNumber, 2).Value = group.CustomerCode ?? string.Empty;
                worksheet.Cell(rowNumber, 3).Value = group.EngagementName;
                worksheet.Cell(rowNumber, 4).Value = item.PlanId;
                worksheet.Cell(rowNumber, 5).Value = item.Sequence;
                worksheet.Cell(rowNumber, 6).Value = GetStatusDisplay(item.Status);
                worksheet.Cell(rowNumber, 7).Value = item.PlanType.ToString();
                worksheet.Cell(rowNumber, 8).Value = item.Percentage;
                worksheet.Cell(rowNumber, 8).Style.NumberFormat.Format = "0.0000";
                worksheet.Cell(rowNumber, 9).Value = item.Amount;
                worksheet.Cell(rowNumber, 9).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(rowNumber, 10).Value = item.BaseValue ?? 0m;
                worksheet.Cell(rowNumber, 10).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(rowNumber, 11).Value = item.EmissionDate;
                worksheet.Cell(rowNumber, 11).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(rowNumber, 12).Value = item.DueDate;
                worksheet.Cell(rowNumber, 12).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(rowNumber, 13).Value = item.RequestDate;
                worksheet.Cell(rowNumber, 13).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(rowNumber, 14).Value = item.EmittedAt;
                worksheet.Cell(rowNumber, 14).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(rowNumber, 15).Value = item.BzCode ?? string.Empty;
                worksheet.Cell(rowNumber, 16).Value = item.RitmNumber ?? string.Empty;
                worksheet.Cell(rowNumber, 17).Value = item.CanceledAt;
                worksheet.Cell(rowNumber, 17).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(rowNumber, 18).Value = item.CancelReason ?? string.Empty;

                rowNumber++;
            }
        }

        var totalsRow = worksheet.Row(rowNumber);
        totalsRow.Cell(1).Value = LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Totals");
        totalsRow.Cell(1).Style.Font.Bold = true;
        totalsRow.Cell(9).FormulaA1 = $"=SUM(I2:I{rowNumber - 1})";
        totalsRow.Cell(9).Style.NumberFormat.Format = "#,##0.00";
        totalsRow.Cell(8).FormulaA1 = $"=SUM(H2:H{rowNumber - 1})";
        totalsRow.Cell(8).Style.NumberFormat.Format = "0.0000";

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] CreatePdf(InvoiceSummaryResult result)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(ComposeHeader);
                page.Content().Element(content => ComposeTable(content, result));
            });
        }).GeneratePdf();
    }

    public string BuildFileName(string entity, string extension)
    {
        if (string.IsNullOrWhiteSpace(entity))
        {
            throw new ArgumentException("Entity is required.", nameof(entity));
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("Extension is required.", nameof(extension));
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
        return $"Export_{entity}_{timestamp}.{extension.TrimStart('.')}";
    }

    private static void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(LocalizationRegistry.Get("Exports.InvoiceSummary.PdfTitle")).FontSize(18).Bold().FontColor("#00338D");
            row.ConstantItem(80).AlignRight().Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        });
    }

    private static void ComposeTable(IContainer container, InvoiceSummaryResult result)
    {
        container.Table(table =>
        {
            var headers = new[]
            {
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Customer"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Engagement"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.PlanId"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Line"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Status"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.PlanType"),
                LocalizationRegistry.Get("InvoiceSummary.TableHeader.Percent"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Amount"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.EmissionDate"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.DueDate"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.RequestDate"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.EmittedAt"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.BzCode"),
                LocalizationRegistry.Get("Exports.InvoiceSummary.Header.Ritm"),
            };

            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(140);
                columns.ConstantColumn(160);
                columns.ConstantColumn(60);
                columns.ConstantColumn(50);
                columns.ConstantColumn(75);
                columns.ConstantColumn(80);
                columns.ConstantColumn(60);
                columns.ConstantColumn(80);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(60);
                columns.ConstantColumn(80);
            });

            table.Header(header =>
            {
                foreach (var title in headers)
                {
                    header.Cell().Element(HeaderStyle).Text(title);
                }
            });

            foreach (var group in result.Groups)
            {
                table.Cell().ColumnSpan((uint)headers.Length).Element(GroupHeaderStyle).Text($"{group.CustomerName ?? ""} – {group.EngagementName}");

                foreach (var item in group.Items)
                {
                    table.Cell().Element(CellStyle).Text(group.CustomerName ?? string.Empty);
                    table.Cell().Element(CellStyle).Text(group.EngagementName);
                    table.Cell().Element(CellStyle).Text(item.PlanId.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(CellStyle).Text(item.Sequence.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(CellStyle).Text(GetStatusDisplay(item.Status));
                    table.Cell().Element(CellStyle).Text(item.PlanType.ToString());
                    table.Cell().Element(CellStyle).Text(item.Percentage.ToString("0.0000", CultureInfo.InvariantCulture));
                    table.Cell().Element(CellStyle).Text(item.Amount.ToString("#,##0.00", CultureInfo.InvariantCulture));
                    table.Cell().Element(CellStyle).Text(FormatDate(item.EmissionDate));
                    table.Cell().Element(CellStyle).Text(FormatDate(item.DueDate));
                    table.Cell().Element(CellStyle).Text(FormatDate(item.RequestDate));
                    table.Cell().Element(CellStyle).Text(FormatDate(item.EmittedAt));
                    table.Cell().Element(CellStyle).Text(item.BzCode ?? string.Empty);
                    table.Cell().Element(CellStyle).Text(item.RitmNumber ?? string.Empty);
                }
            }
        });
    }

    private static IContainer HeaderStyle(IContainer container)
    {
        return container.Background("#00338D").Padding(6).DefaultTextStyle(style => style.FontColor(Colors.White).Bold());
    }

    private static IContainer GroupHeaderStyle(IContainer container)
    {
        return container.Background("#E5ECFF").PaddingVertical(4).PaddingHorizontal(6).DefaultTextStyle(style => style.SemiBold());
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.Padding(4);
    }

    private static string FormatDate(DateTime? value)
    {
        return value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string GetStatusDisplay(InvoiceItemStatus status)
    {
        return status switch
        {
            InvoiceItemStatus.Planned => LocalizationRegistry.Get("Invoice.Status.Planned"),
            InvoiceItemStatus.Requested => LocalizationRegistry.Get("Invoice.Status.Requested"),
            InvoiceItemStatus.Emitted => LocalizationRegistry.Get("Invoice.Status.Emitted"),
            InvoiceItemStatus.Closed => LocalizationRegistry.Get("Invoice.Status.Closed"),
            InvoiceItemStatus.Canceled => LocalizationRegistry.Get("Invoice.Status.Canceled"),
            InvoiceItemStatus.Reissued => LocalizationRegistry.Get("Invoice.Status.Reissued"),
            _ => status.ToString(),
        };
    }
}
