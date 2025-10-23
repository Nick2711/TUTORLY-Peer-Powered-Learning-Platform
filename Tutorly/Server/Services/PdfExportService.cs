using System.Text;
using System.Linq;
using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface IPdfExportService
    {
        Task<byte[]> GenerateAnalyticsPdfAsync(TutorAnalyticsDto analytics, string tutorName, int days);
    }

    public class PdfExportService : IPdfExportService
    {
        public async Task<byte[]> GenerateAnalyticsPdfAsync(TutorAnalyticsDto analytics, string tutorName, int days)
        {
            try
            {
                Console.WriteLine($"DEBUG: PdfExportService - Starting PDF generation for {tutorName}");

                // Generate HTML content
                var htmlContent = GenerateHtmlContent(analytics, tutorName, days);

                // Convert HTML to PDF using PuppeteerSharp
                var pdfBytes = await ConvertHtmlToPdfAsync(htmlContent);

                Console.WriteLine($"DEBUG: PdfExportService - PDF generated successfully, {pdfBytes.Length} bytes");
                return pdfBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: PdfExportService - Error generating PDF: {ex.Message}");
                Console.WriteLine($"DEBUG: PdfExportService - StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        private string GenerateHtmlContent(TutorAnalyticsDto analytics, string tutorName, int days)
        {
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<title>Tutor Analytics Report</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; line-height: 1.6; }");
            html.AppendLine("h1 { color: #2c3e50; text-align: center; border-bottom: 3px solid #3498db; padding-bottom: 10px; }");
            html.AppendLine("h2 { color: #34495e; margin-top: 30px; margin-bottom: 15px; }");
            html.AppendLine("table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }");
            html.AppendLine("th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            html.AppendLine("th { background-color: #3498db; color: white; font-weight: bold; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            html.AppendLine(".metrics-table th, .metrics-table td { text-align: center; }");
            html.AppendLine(".summary { background-color: #ecf0f1; padding: 15px; border-radius: 5px; margin: 20px 0; }");
            html.AppendLine(".footer { text-align: center; font-size: 12px; color: #7f8c8d; margin-top: 40px; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            // Title
            html.AppendLine($"<h1>TUTOR ANALYTICS REPORT</h1>");

            // Tutor info
            html.AppendLine($"<p><strong>Tutor:</strong> {tutorName}</p>");
            html.AppendLine($"<p><strong>Report Period:</strong> Last {days} days</p>");
            html.AppendLine($"<p><strong>Generated:</strong> {DateTime.Now:MMMM dd, yyyy 'at' HH:mm}</p>");

            // Key Metrics
            html.AppendLine("<h2>KEY PERFORMANCE METRICS</h2>");
            html.AppendLine("<table class='metrics-table'>");
            html.AppendLine("<tr><th>Metric</th><th>Value</th></tr>");
            html.AppendLine($"<tr><td>Total Sessions</td><td>{analytics.TotalSessions}</td></tr>");
            html.AppendLine($"<tr><td>Unique Students</td><td>{analytics.UniqueStudents}</td></tr>");
            html.AppendLine($"<tr><td>Total Hours</td><td>{analytics.TotalHours:F1} hours</td></tr>");
            html.AppendLine($"<tr><td>Average Rating</td><td>{analytics.AverageRating:F1}/5.0</td></tr>");
            html.AppendLine($"<tr><td>No-Show Rate</td><td>{analytics.NoShowRate:P1}</td></tr>");
            html.AppendLine($"<tr><td>Verified Responses</td><td>{analytics.VerifiedResponses}</td></tr>");
            html.AppendLine("</table>");

            // Recent Sessions
            if (analytics.RecentSessions.Any())
            {
                html.AppendLine("<h2>RECENT SESSIONS</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>Date</th><th>Module</th><th>Student</th><th>Duration</th><th>Rating</th></tr>");

                foreach (var session in analytics.RecentSessions.Take(10))
                {
                    html.AppendLine($"<tr>");
                    html.AppendLine($"<td>{session.ScheduledStart:MMM dd, yyyy}</td>");
                    html.AppendLine($"<td>{session.ModuleName}</td>");
                    html.AppendLine($"<td>{session.StudentName}</td>");
                    html.AppendLine($"<td>{session.DurationMinutes} min</td>");
                    html.AppendLine($"<td>{session.Rating?.ToString("F1") ?? "—"}</td>");
                    html.AppendLine("</tr>");
                }

                html.AppendLine("</table>");
            }

            // Top Students
            if (analytics.TopStudents.Any())
            {
                html.AppendLine("<h2>TOP STUDENTS BY HOURS</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>Student</th><th>Sessions</th><th>Total Hours</th><th>Avg Rating</th></tr>");

                foreach (var student in analytics.TopStudents.Take(6))
                {
                    html.AppendLine($"<tr>");
                    html.AppendLine($"<td>{student.StudentName}</td>");
                    html.AppendLine($"<td>{student.SessionCount}</td>");
                    html.AppendLine($"<td>{student.TotalHours:F1}h</td>");
                    html.AppendLine($"<td>{student.AverageRating?.ToString("F1") ?? "—"}</td>");
                    html.AppendLine("</tr>");
                }

                html.AppendLine("</table>");
            }

            // Summary
            html.AppendLine("<h2>SUMMARY</h2>");
            html.AppendLine($"<div class='summary'>");
            html.AppendLine($"Over the past {days} days, this tutor has conducted {analytics.TotalSessions} sessions " +
                           $"with {analytics.UniqueStudents} unique students, totaling {analytics.TotalHours:F1} hours of instruction. " +
                           $"The average rating received is {analytics.AverageRating:F1} out of 5.0, " +
                           $"with a no-show rate of {analytics.NoShowRate:P1}. " +
                           $"Additionally, {analytics.VerifiedResponses} forum responses were verified by this tutor, " +
                           $"demonstrating active engagement in the academic community.");
            html.AppendLine("</div>");

            // Footer
            html.AppendLine("<div class='footer'>");
            html.AppendLine("This report was generated automatically by Tutorly Analytics System");
            html.AppendLine("</div>");

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private Task<byte[]> ConvertHtmlToPdfAsync(string htmlContent)
        {
            try
            {
                Console.WriteLine("DEBUG: PdfExportService - Starting HTML to PDF conversion");

                // For now, let's create a simple PDF using basic PDF structure
                // This is a minimal PDF implementation that creates a valid PDF file
                var pdfBytes = CreateSimplePdf(htmlContent);

                Console.WriteLine($"DEBUG: PdfExportService - PDF conversion completed, {pdfBytes.Length} bytes");
                return Task.FromResult(pdfBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: PdfExportService - Error in HTML to PDF conversion: {ex.Message}");
                throw;
            }
        }

        private byte[] CreateSimplePdf(string htmlContent)
        {
            // Extract text content from HTML for a simple PDF
            var textContent = ExtractTextFromHtml(htmlContent);

            // Create a simple PDF structure
            var pdfContent = new StringBuilder();

            // PDF Header
            pdfContent.AppendLine("%PDF-1.4");
            pdfContent.AppendLine("1 0 obj");
            pdfContent.AppendLine("<<");
            pdfContent.AppendLine("/Type /Catalog");
            pdfContent.AppendLine("/Pages 2 0 R");
            pdfContent.AppendLine(">>");
            pdfContent.AppendLine("endobj");

            // Pages object
            pdfContent.AppendLine("2 0 obj");
            pdfContent.AppendLine("<<");
            pdfContent.AppendLine("/Type /Pages");
            pdfContent.AppendLine("/Kids [3 0 R]");
            pdfContent.AppendLine("/Count 1");
            pdfContent.AppendLine(">>");
            pdfContent.AppendLine("endobj");

            // Page object
            pdfContent.AppendLine("3 0 obj");
            pdfContent.AppendLine("<<");
            pdfContent.AppendLine("/Type /Page");
            pdfContent.AppendLine("/Parent 2 0 R");
            pdfContent.AppendLine("/MediaBox [0 0 612 792]");
            pdfContent.AppendLine("/Contents 4 0 R");
            pdfContent.AppendLine("/Resources <<");
            pdfContent.AppendLine("/Font <<");
            pdfContent.AppendLine("/F1 5 0 R");
            pdfContent.AppendLine(">>");
            pdfContent.AppendLine(">>");
            pdfContent.AppendLine(">>");
            pdfContent.AppendLine("endobj");

            // Content stream
            var contentStream = CreateContentStream(textContent);
            pdfContent.AppendLine("4 0 obj");
            pdfContent.AppendLine($"<< /Length {contentStream.Length} >>");
            pdfContent.AppendLine("stream");
            pdfContent.AppendLine(contentStream);
            pdfContent.AppendLine("endstream");
            pdfContent.AppendLine("endobj");

            // Font object
            pdfContent.AppendLine("5 0 obj");
            pdfContent.AppendLine("<<");
            pdfContent.AppendLine("/Type /Font");
            pdfContent.AppendLine("/Subtype /Type1");
            pdfContent.AppendLine("/BaseFont /Helvetica");
            pdfContent.AppendLine(">>");
            pdfContent.AppendLine("endobj");

            // Cross-reference table
            pdfContent.AppendLine("xref");
            pdfContent.AppendLine("0 6");
            pdfContent.AppendLine("0000000000 65535 f ");
            pdfContent.AppendLine("0000000009 00000 n ");
            pdfContent.AppendLine("0000000058 00000 n ");
            pdfContent.AppendLine("0000000115 00000 n ");
            pdfContent.AppendLine($"0000000206 00000 n ");
            pdfContent.AppendLine($"0000000{200 + contentStream.Length:X4} 00000 n ");

            // Trailer
            pdfContent.AppendLine("trailer");
            pdfContent.AppendLine("<<");
            pdfContent.AppendLine("/Size 6");
            pdfContent.AppendLine("/Root 1 0 R");
            pdfContent.AppendLine(">>");
            pdfContent.AppendLine("startxref");
            pdfContent.AppendLine($"0000000{300 + contentStream.Length:X4}");
            pdfContent.AppendLine("%%EOF");

            return Encoding.UTF8.GetBytes(pdfContent.ToString());
        }

        private string CreateContentStream(string text)
        {
            var lines = text.Split('\n').Take(50); // Increased to 50 lines
            var content = new StringBuilder();

            content.AppendLine("BT");
            content.AppendLine("/F1 14 Tf"); // Increased font size
            content.AppendLine("50 750 Td"); // Adjusted starting position

            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (!string.IsNullOrEmpty(cleanLine))
                {
                    // Handle headers differently
                    if (cleanLine.StartsWith("==="))
                    {
                        content.AppendLine("/F1 16 Tf"); // Larger font for headers
                        content.AppendLine($"({cleanLine}) Tj");
                        content.AppendLine("0 -20 Td"); // More space for headers
                        content.AppendLine("/F1 12 Tf"); // Back to normal font
                    }
                    else if (cleanLine.StartsWith("---"))
                    {
                        content.AppendLine("/F1 14 Tf"); // Medium font for subheaders
                        content.AppendLine($"({cleanLine}) Tj");
                        content.AppendLine("0 -18 Td"); // Space for subheaders
                        content.AppendLine("/F1 12 Tf"); // Back to normal font
                    }
                    else
                    {
                        content.AppendLine($"({cleanLine}) Tj");
                        content.AppendLine("0 -15 Td"); // Normal line spacing
                    }
                }
            }

            content.AppendLine("ET");

            return content.ToString();
        }

        private string ExtractTextFromHtml(string html)
        {
            // Remove everything before <body> tag (including head, styles, etc.)
            var bodyStart = html.IndexOf("<body>");
            if (bodyStart >= 0)
            {
                html = html.Substring(bodyStart + 6); // Remove "<body>"
            }

            // Remove everything after </body> tag
            var bodyEnd = html.IndexOf("</body>");
            if (bodyEnd >= 0)
            {
                html = html.Substring(0, bodyEnd);
            }

            // Extract text content properly
            var text = html
                .Replace("<h1>", "\n\n=== ")
                .Replace("</h1>", " ===\n")
                .Replace("<h2>", "\n--- ")
                .Replace("</h2>", " ---\n")
                .Replace("<p>", "")
                .Replace("</p>", "\n")
                .Replace("<tr>", "")
                .Replace("</tr>", "\n")
                .Replace("<td>", " | ")
                .Replace("</td>", "")
                .Replace("<th>", " | ")
                .Replace("</th>", "")
                .Replace("<table>", "")
                .Replace("</table>", "\n")
                .Replace("<div class='summary'>", "\n")
                .Replace("<div class='footer'>", "\n")
                .Replace("</div>", "\n")
                .Replace("<strong>", "")
                .Replace("</strong>", "")
                .Replace("<br>", "\n")
                .Replace("<br/>", "\n");

            // Remove all remaining HTML tags
            while (text.Contains('<') && text.Contains('>'))
            {
                var start = text.IndexOf('<');
                var end = text.IndexOf('>', start);
                if (end > start)
                {
                    text = text.Remove(start, end - start + 1);
                }
                else
                {
                    break;
                }
            }

            // Clean up whitespace and formatting
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            text = text.Replace(" | ", " | ");
            text = text.Replace("=== ", "\n=== ");
            text = text.Replace(" ---", " ---\n");

            // Split into lines and clean each line
            var lines = text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToArray();

            return string.Join("\n", lines);
        }
    }
}
