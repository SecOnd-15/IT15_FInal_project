using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Latog_Final_project.Models;

namespace Latog_Final_project.Services
{
    public class InvoicePdfService
    {
        private readonly IWebHostEnvironment _env;

        public InvoicePdfService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public byte[] GeneratePdf(Invoice invoice)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                          
                            var logoPath = Path.Combine(_env.WebRootPath, "images", "irp-logo.png");
                            if (File.Exists(logoPath))
                            {
                                row.ConstantItem(80).Image(logoPath);
                            }

                            row.RelativeItem().PaddingLeft(15).Column(headerCol =>
                            {
                                headerCol.Item().Text("IRPMS — Invoice Receipt")
                                    .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

                                headerCol.Item().Text("Integrated Resource & Procurement Management System")
                                    .FontSize(9).FontColor(Colors.Grey.Medium);
                            });

                            row.ConstantItem(140).AlignRight().Column(invCol =>
                            {
                                invCol.Item().Text(invoice.InvoiceNumber)
                                    .FontSize(14).Bold().FontColor("#FFC107");

                                var statusColor = invoice.PaymentStatus switch
                                {
                                    "Paid" => Colors.Green.Darken1,
                                    "Partially Paid" => Colors.Orange.Darken1,
                                    _ => Colors.Red.Darken1
                                };

                                invCol.Item().PaddingTop(4).Text(invoice.PaymentStatus.ToUpper())
                                    .FontSize(10).Bold().FontColor(statusColor);
                            });
                        });

                        col.Item().PaddingVertical(10)
                            .LineHorizontal(2).LineColor("#FFC107");
                    });

                   
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                      
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Supplier").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(invoice.Supplier).Bold().FontSize(13);
                            });

                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("Created Date").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(invoice.CreatedDate.ToString("MMMM dd, yyyy")).Bold();
                            });
                        });

                        col.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Due Date").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(invoice.DueDate?.ToString("MMMM dd, yyyy") ?? "Not Set").Bold();
                            });

                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                if (invoice.PaidDate.HasValue)
                                {
                                    c.Item().Text("Paid Date").FontSize(9).FontColor(Colors.Grey.Medium);
                                    c.Item().Text(invoice.PaidDate.Value.ToString("MMMM dd, yyyy")).Bold().FontColor(Colors.Green.Darken1);
                                }
                            });
                        });

                        col.Item().PaddingVertical(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                       
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(4); 
                                columns.RelativeColumn(1); 
                                columns.RelativeColumn(2); 
                            });

                           
                            table.Header(header =>
                            {
                                header.Cell().Background("#FFC107").Padding(8)
                                    .Text("Item Description").Bold().FontColor(Colors.Black);
                                header.Cell().Background("#FFC107").Padding(8)
                                    .Text("Qty").Bold().FontColor(Colors.Black);
                                header.Cell().Background("#FFC107").Padding(8).AlignRight()
                                    .Text("Amount").Bold().FontColor(Colors.Black);
                            });

                         
                            var itemName = invoice.ResourceRequest?.ItemName ?? "N/A";
                            var qty = invoice.ResourceRequest?.Quantity ?? 0;

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                .Text(itemName);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                .Text(qty.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignRight()
                                .Text($"₱{invoice.TotalAmount:N2}").Bold();
                        });

                        col.Item().PaddingTop(10);

                       
                        col.Item().AlignRight().Width(250).Column(summary =>
                        {
                            summary.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Total Amount:").Bold();
                                row.ConstantItem(120).AlignRight().Text($"₱{invoice.TotalAmount:N2}").Bold().FontSize(13);
                            });

                            summary.Item().PaddingTop(4).Row(row =>
                            {
                                row.RelativeItem().Text("Amount Paid:");
                                row.ConstantItem(120).AlignRight().Text($"₱{invoice.AmountPaid:N2}")
                                    .FontColor(Colors.Green.Darken1);
                            });

                            var balance = invoice.TotalAmount - invoice.AmountPaid;
                            summary.Item().PaddingTop(4).Row(row =>
                            {
                                row.RelativeItem().Text("Balance Due:").Bold();
                                row.ConstantItem(120).AlignRight().Text($"₱{balance:N2}")
                                    .Bold().FontSize(13)
                                    .FontColor(balance > 0 ? Colors.Red.Darken1 : Colors.Green.Darken1);
                            });
                        });

                        
                        if (!string.IsNullOrEmpty(invoice.Notes))
                        {
                            col.Item().PaddingTop(20).Column(noteCol =>
                            {
                                noteCol.Item().Text("Notes:").Bold().FontSize(10).FontColor(Colors.Grey.Darken1);
                                noteCol.Item().PaddingTop(4).Text(invoice.Notes).FontSize(10).Italic();
                            });
                        }
                    });

                    
                    page.Footer().AlignCenter().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(8).Text(text =>
                        {
                            text.Span("Generated by ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.Span("IRPMS").FontSize(8).Bold().FontColor("#FFC107");
                            text.Span($" on {DateTime.Now:MMMM dd, yyyy hh:mm tt}").FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
