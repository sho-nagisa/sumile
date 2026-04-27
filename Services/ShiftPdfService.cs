using Microsoft.EntityFrameworkCore;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using sumile.Data;
using sumile.Models;
using System.Globalization;

namespace sumile.Services
{
    public class ShiftPdfService
    {
        private const int MaxDaysPerPage = 10;
        private const string FontFamily = "NotoSansJP";
        private const double Margin = 8;
        private const double UserColumnWidth = 94;
        private const double RoleColumnWidth = 30;
        private const double HeaderRowHeight = 23;
        private const double UserRowHeight = 24;
        private const double SummaryRowHeight = 24;

        private const string TitleText = "\u30b7\u30d5\u30c8\u8868";
        private const string UserText = "\u30e6\u30fc\u30b6\u30fc";
        private const string KeyText = "\u9375";
        private const string MorningText = "\u4e0a";
        private const string NightText = "\u6577";
        private const string RequiredWorkersText = "\u5fc5\u8981\u4eba\u6570";
        private const string AcceptedText = "\u25cb";
        private const string KeyHolderAcceptedText = "\u8d64\u4e38";
        private const string RemainingText = "\u6b8b\u308a";
        private const string StarText = "\u2605";
        private const string EmptyPeriodMessage =
            "\u3053\u306e\u671f\u9593\u306e\u30b7\u30d5\u30c8\u65e5\u306f\u307e\u3060\u767b\u9332\u3055\u308c\u3066\u3044\u307e\u305b\u3093\u3002";

        private static readonly XPdfFontOptions UnicodeFontOptions =
            new(PdfFontEncoding.Unicode);

        private static readonly XPen GridPen =
            new(new XColor { R = 218, G = 224, B = 230 }, 0.55);

        private static readonly XBrush HeaderBrush =
            new XSolidBrush(new XColor { R = 248, G = 249, B = 250 });

        private static readonly XBrush SummaryBrush =
            new XSolidBrush(new XColor { R = 250, G = 251, B = 252 });

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ShiftTableService _shiftTableService;

        public ShiftPdfService(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            ShiftTableService shiftTableService)
        {
            _context = context;
            _env = env;
            _shiftTableService = shiftTableService;
        }

        public async Task<string> GenerateShiftPdfAsync(int periodId)
        {
            var period = await _context.RecruitmentPeriods
                .FirstOrDefaultAsync(r => r.Id == periodId);

            if (period == null)
            {
                throw new InvalidOperationException("\u52df\u96c6\u671f\u9593\u304c\u898b\u3064\u304b\u308a\u307e\u305b\u3093\u3002");
            }

            var table = await _shiftTableService.BuildAsync(periodId);
            var users = await _context.Users
                .OrderBy(u => u.CustomId)
                .Select(u => new ShiftPdfUser
                {
                    Id = u.Id,
                    CustomId = u.CustomId,
                    Name = u.Name,
                    UserShiftRole = u.UserShiftRole
                })
                .ToListAsync();

            var document = new PdfDocument();
            document.Info.Title = $"{TitleText} {period.StartDate:yyyy/MM/dd}-{period.EndDate:yyyy/MM/dd}";
            document.Info.Subject = $"\u52df\u96c6\u671f\u9593ID: {periodId}";

            if (!table.ShiftDays.Any() || !table.ShiftColumns.Any())
            {
                DrawEmptyPdf(document, period);
            }
            else
            {
                DrawShiftTablePdf(document, period, table, users);
            }

            var filePath = GetShiftPdfPhysicalPath(periodId);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using var fs = new FileStream(filePath, FileMode.Create);
            document.Save(fs);

            return GetShiftPdfRelativePath(periodId);
        }

        public string GetShiftPdfRelativePath(int periodId)
        {
            return $"/shift_pdfs/shift_{periodId}.pdf";
        }

        public string GetShiftPdfPhysicalPath(int periodId)
        {
            return Path.Combine(_env.WebRootPath, "shift_pdfs", $"shift_{periodId}.pdf");
        }

        public async Task<string> EnsureShiftPdfAsync(int periodId)
        {
            if (await NeedsRegenerationAsync(periodId))
            {
                return await GenerateShiftPdfAsync(periodId);
            }

            return GetShiftPdfRelativePath(periodId);
        }

        private async Task<bool> NeedsRegenerationAsync(int periodId)
        {
            var filePath = GetShiftPdfPhysicalPath(periodId);
            if (!File.Exists(filePath))
            {
                return true;
            }

            var pdfUpdatedAt = File.GetLastWriteTimeUtc(filePath);
            var appUpdatedAt = File.GetLastWriteTimeUtc(typeof(ShiftPdfService).Assembly.Location);
            if (pdfUpdatedAt < appUpdatedAt)
            {
                return true;
            }

            var latestSubmissionAt = await _context.ShiftSubmissions
                .Where(s => s.ShiftDay.RecruitmentPeriodId == periodId)
                .MaxAsync(s => s.SubmittedAt);

            if (latestSubmissionAt.HasValue && latestSubmissionAt.Value > pdfUpdatedAt)
            {
                return true;
            }

            var latestEditAt = await _context.ShiftEditLogs
                .Where(l => l.ShiftDay.RecruitmentPeriodId == periodId)
                .Select(l => (DateTime?)l.EditDate)
                .MaxAsync();

            return latestEditAt.HasValue && latestEditAt.Value > pdfUpdatedAt;
        }

        private static void DrawEmptyPdf(PdfDocument document, RecruitmentPeriod period)
        {
            var page = AddPage(document);
            using var gfx = XGraphics.FromPdfPage(page);

            var titleFont = CreateFont(16, XFontStyle.Bold);
            var bodyFont = CreateFont(10, XFontStyle.Regular);

            gfx.DrawString(TitleText, titleFont, XBrushes.Black, new XPoint(36, 48));
            gfx.DrawString(
                $"{period.StartDate:yyyy/MM/dd} - {period.EndDate:yyyy/MM/dd}",
                bodyFont,
                XBrushes.Black,
                new XPoint(36, 72));
            gfx.DrawString(
                EmptyPeriodMessage,
                bodyFont,
                XBrushes.Gray,
                new XPoint(36, 104));
        }

        private static void DrawShiftTablePdf(
            PdfDocument document,
            RecruitmentPeriod period,
            ShiftTableResult table,
            List<ShiftPdfUser> users)
        {
            var columnChunks = SplitColumnsByDay(table);
            var usersPerPage = CalculateUsersPerPage();
            var userChunks = SplitUsers(users, usersPerPage);
            if (!userChunks.Any())
            {
                userChunks.Add(new List<ShiftPdfUser>());
            }

            var submissionMap = table.Submissions
                .GroupBy(s => (s.UserId, s.ShiftDayId, s.ShiftType))
                .ToDictionary(g => g.Key, g => g.First());

            var pageNumber = 1;
            var pageCount = columnChunks.Count * userChunks.Count;

            foreach (var columnChunk in columnChunks)
            {
                foreach (var userChunk in userChunks)
                {
                    var page = AddPage(document);
                    using var gfx = XGraphics.FromPdfPage(page);

                    var layout = CreateLayout(page, columnChunk.Columns.Count);
                    DrawTableHeader(gfx, layout, columnChunk);
                    DrawUserRows(gfx, layout, columnChunk, userChunk, submissionMap);
                    DrawSummaryRows(gfx, layout, table, columnChunk, userChunk.Count);
                    DrawPageFooter(gfx, page, period, pageNumber, pageCount);

                    pageNumber++;
                }
            }
        }

        private static PdfPage AddPage(PdfDocument document)
        {
            var page = document.AddPage();
            page.Size = PageSize.A4;
            page.Orientation = PageOrientation.Landscape;
            return page;
        }

        private static ShiftPdfLayout CreateLayout(PdfPage page, int columnCount)
        {
            var fixedWidth = UserColumnWidth + RoleColumnWidth;
            var availableWidth = page.Width - (Margin * 2) - fixedWidth;
            var shiftColumnWidth = availableWidth / Math.Max(1, columnCount);

            return new ShiftPdfLayout
            {
                Left = Margin,
                Top = Margin,
                Width = page.Width - (Margin * 2),
                FixedWidth = fixedWidth,
                ShiftColumnWidth = shiftColumnWidth
            };
        }

        private static void DrawTableHeader(
            XGraphics gfx,
            ShiftPdfLayout layout,
            ShiftColumnChunk columnChunk)
        {
            var headerFont = CreateFont(7.5, XFontStyle.Bold);
            var smallFont = CreateFont(6.8, XFontStyle.Bold);
            var numberFont = CreateFont(6.8, XFontStyle.Regular);
            var fixedHeaderHeight = HeaderRowHeight * 4;

            DrawCell(gfx, layout.Left, layout.Top, UserColumnWidth, fixedHeaderHeight, HeaderBrush);
            DrawCell(gfx, layout.Left + UserColumnWidth, layout.Top, RoleColumnWidth, fixedHeaderHeight, HeaderBrush);
            DrawText(gfx, UserText, headerFont, XBrushes.Black, layout.Left, layout.Top, UserColumnWidth, fixedHeaderHeight, XStringFormats.Center);
            DrawText(gfx, KeyText, headerFont, XBrushes.Black, layout.Left + UserColumnWidth, layout.Top, RoleColumnWidth, fixedHeaderHeight, XStringFormats.Center);

            foreach (var dayGroup in columnChunk.Columns.GroupBy(c => c.Date.Date))
            {
                var firstIndex = columnChunk.Columns.FindIndex(c => c.ShiftDayId == dayGroup.First().ShiftDayId);
                var span = dayGroup.Count();
                var x = layout.FirstShiftColumnLeft + (firstIndex * layout.ShiftColumnWidth);
                var width = span * layout.ShiftColumnWidth;

                DrawCell(gfx, x, layout.Top, width, HeaderRowHeight);
                DrawCell(gfx, x, layout.Top + HeaderRowHeight, width, HeaderRowHeight);
                DrawText(gfx, dayGroup.Key.ToString("M/d"), headerFont, XBrushes.Black, x, layout.Top, width, HeaderRowHeight, XStringFormats.Center);
                DrawText(
                    gfx,
                    dayGroup.Key.ToString("ddd", new CultureInfo("ja-JP")),
                    smallFont,
                    XBrushes.Black,
                    x,
                    layout.Top + HeaderRowHeight,
                    width,
                    HeaderRowHeight,
                    XStringFormats.Center);
            }

            for (var i = 0; i < columnChunk.Columns.Count; i++)
            {
                var column = columnChunk.Columns[i];
                var x = layout.FirstShiftColumnLeft + (i * layout.ShiftColumnWidth);
                DrawCell(gfx, x, layout.Top + (HeaderRowHeight * 2), layout.ShiftColumnWidth, HeaderRowHeight);
                DrawText(
                    gfx,
                    column.ShiftType == ShiftType.Morning ? MorningText : NightText,
                    headerFont,
                    XBrushes.Black,
                    x,
                    layout.Top + (HeaderRowHeight * 2),
                    layout.ShiftColumnWidth,
                    HeaderRowHeight,
                    XStringFormats.Center);
            }

            DrawWorkloadRow(gfx, layout, columnChunk, numberFont);
        }

        private static void DrawWorkloadRow(
            XGraphics gfx,
            ShiftPdfLayout layout,
            ShiftColumnChunk columnChunk,
            XFont numberFont)
        {
            var y = layout.Top + (HeaderRowHeight * 3);

            foreach (var cell in columnChunk.WorkloadCells)
            {
                var visibleStart = Math.Max(cell.StartIndex, columnChunk.StartIndex);
                var visibleEnd = Math.Min(cell.EndIndex, columnChunk.EndIndex);
                if (visibleStart >= visibleEnd)
                {
                    continue;
                }

                var x = layout.FirstShiftColumnLeft + ((visibleStart - columnChunk.StartIndex) * layout.ShiftColumnWidth);
                var width = (visibleEnd - visibleStart) * layout.ShiftColumnWidth;
                DrawCell(gfx, x, y, width, HeaderRowHeight);
                DrawText(gfx, cell.RequiredCount.ToString(), numberFont, XBrushes.Black, x, y, width, HeaderRowHeight, XStringFormats.Center);
            }
        }

        private static void DrawUserRows(
            XGraphics gfx,
            ShiftPdfLayout layout,
            ShiftColumnChunk columnChunk,
            List<ShiftPdfUser> users,
            IReadOnlyDictionary<(string UserId, int ShiftDayId, ShiftType ShiftType), ShiftSubmission> submissionMap)
        {
            var userFont = CreateFont(7.4, XFontStyle.Regular);
            var symbolFont = CreateFont(8.2, XFontStyle.Regular);
            var keyFont = CreateFont(8.6, XFontStyle.Bold);
            var y = layout.UserRowsTop;

            foreach (var user in users)
            {
                DrawCell(gfx, layout.Left, y, UserColumnWidth, UserRowHeight);
                DrawCell(gfx, layout.Left + UserColumnWidth, y, RoleColumnWidth, UserRowHeight);
                DrawText(
                    gfx,
                    string.IsNullOrWhiteSpace(user.Name) ? user.CustomId.ToString() : user.Name,
                    userFont,
                    XBrushes.Black,
                    layout.Left + 3,
                    y,
                    UserColumnWidth - 6,
                    UserRowHeight,
                    XStringFormats.CenterLeft);
                DrawText(
                    gfx,
                    user.UserShiftRole == UserShiftRole.KeyHolder ? StarText : "",
                    keyFont,
                    XBrushes.Black,
                    layout.Left + UserColumnWidth,
                    y,
                    RoleColumnWidth,
                    UserRowHeight,
                    XStringFormats.Center);

                for (var i = 0; i < columnChunk.Columns.Count; i++)
                {
                    var column = columnChunk.Columns[i];
                    submissionMap.TryGetValue((user.Id, column.ShiftDayId, column.ShiftType), out var submission);

                    var x = layout.FirstShiftColumnLeft + (i * layout.ShiftColumnWidth);
                    DrawCell(gfx, x, y, layout.ShiftColumnWidth, UserRowHeight);
                    DrawText(
                        gfx,
                        GetShiftStatusSymbol(submission?.ShiftStatus),
                        symbolFont,
                        GetShiftBrush(submission, user),
                        x,
                        y,
                        layout.ShiftColumnWidth,
                        UserRowHeight,
                        XStringFormats.Center);
                }

                y += UserRowHeight;
            }
        }

        private static void DrawSummaryRows(
            XGraphics gfx,
            ShiftPdfLayout layout,
            ShiftTableResult table,
            ShiftColumnChunk columnChunk,
            int userCount)
        {
            var labels = new[] { RequiredWorkersText, AcceptedText, KeyHolderAcceptedText, RemainingText };
            var values = new[]
            {
                table.RequiredWorkersList,
                table.TotalAcceptedList,
                table.KeyHolderAcceptedList,
                table.RemainingWorkersList
            };

            var labelFont = CreateFont(7.2, XFontStyle.Regular);
            var valueFont = CreateFont(7.2, XFontStyle.Regular);
            var y = layout.UserRowsTop + (userCount * UserRowHeight);

            for (var row = 0; row < labels.Length; row++)
            {
                DrawCell(gfx, layout.Left, y, layout.FixedWidth, SummaryRowHeight, SummaryBrush);
                DrawText(gfx, labels[row], labelFont, XBrushes.Black, layout.Left, y, layout.FixedWidth, SummaryRowHeight, XStringFormats.Center);

                for (var i = 0; i < columnChunk.Columns.Count; i++)
                {
                    var valueIndex = columnChunk.StartIndex + i;
                    var value = valueIndex < values[row].Count ? values[row][valueIndex] : 0;
                    var x = layout.FirstShiftColumnLeft + (i * layout.ShiftColumnWidth);
                    var brush = row == 3 && value < 0 ? XBrushes.Red : XBrushes.Black;

                    DrawCell(gfx, x, y, layout.ShiftColumnWidth, SummaryRowHeight);
                    DrawText(gfx, value.ToString(), valueFont, brush, x, y, layout.ShiftColumnWidth, SummaryRowHeight, XStringFormats.Center);
                }

                y += SummaryRowHeight;
            }
        }

        private static void DrawPageFooter(
            XGraphics gfx,
            PdfPage page,
            RecruitmentPeriod period,
            int pageNumber,
            int pageCount)
        {
            var footerFont = CreateFont(6.2, XFontStyle.Regular);
            var text = $"{TitleText} {period.StartDate:yyyy/MM/dd}-{period.EndDate:yyyy/MM/dd}  {pageNumber}/{pageCount}";
            gfx.DrawString(
                text,
                footerFont,
                XBrushes.Gray,
                new XRect(Margin, page.Height - 13, page.Width - (Margin * 2), 8),
                XStringFormats.CenterRight);
        }

        private static List<ShiftColumnChunk> SplitColumnsByDay(ShiftTableResult table)
        {
            var workloadCells = BuildIndexedWorkloadCells(table.WorkloadCells);
            var chunks = new List<ShiftColumnChunk>();

            for (var dayStart = 0; dayStart < table.ShiftDays.Count; dayStart += MaxDaysPerPage)
            {
                var startIndex = dayStart * 2;
                var columns = table.ShiftColumns
                    .Skip(startIndex)
                    .Take(MaxDaysPerPage * 2)
                    .ToList();

                chunks.Add(new ShiftColumnChunk
                {
                    StartIndex = startIndex,
                    Columns = columns,
                    WorkloadCells = workloadCells
                });
            }

            return chunks;
        }

        private static List<IndexedWorkloadCell> BuildIndexedWorkloadCells(List<ShiftTableWorkloadCell> workloadCells)
        {
            var indexedCells = new List<IndexedWorkloadCell>();
            var cursor = 0;

            foreach (var workloadCell in workloadCells)
            {
                indexedCells.Add(new IndexedWorkloadCell
                {
                    StartIndex = cursor,
                    EndIndex = cursor + workloadCell.Colspan,
                    RequiredCount = workloadCell.RequiredCount
                });

                cursor += workloadCell.Colspan;
            }

            return indexedCells;
        }

        private static int CalculateUsersPerPage()
        {
            var usableHeight = A4LandscapeHeight() - (Margin * 2) - 13;
            var fixedHeight = (HeaderRowHeight * 4) + (SummaryRowHeight * 4);
            return Math.Max(1, (int)Math.Floor((usableHeight - fixedHeight) / UserRowHeight));
        }

        private static List<List<ShiftPdfUser>> SplitUsers(List<ShiftPdfUser> users, int usersPerPage)
        {
            var chunks = new List<List<ShiftPdfUser>>();

            for (var start = 0; start < users.Count; start += usersPerPage)
            {
                chunks.Add(users.Skip(start).Take(usersPerPage).ToList());
            }

            return chunks;
        }

        private static double A4LandscapeHeight() => 595;

        private static XFont CreateFont(double size, XFontStyle style)
        {
            return new XFont(FontFamily, size, style, UnicodeFontOptions);
        }

        private static void DrawCell(
            XGraphics gfx,
            double x,
            double y,
            double width,
            double height,
            XBrush? backgroundBrush = null)
        {
            if (backgroundBrush != null)
            {
                gfx.DrawRectangle(backgroundBrush, x, y, width, height);
            }

            gfx.DrawRectangle(GridPen, x, y, width, height);
        }

        private static void DrawText(
            XGraphics gfx,
            string text,
            XFont font,
            XBrush brush,
            double x,
            double y,
            double width,
            double height,
            XStringFormat format)
        {
            var rect = new XRect(x, y, width, height);
            var fittedFont = FitFont(gfx, text, font, width - 4);
            gfx.DrawString(text, fittedFont, brush, rect, format);
        }

        private static XFont FitFont(XGraphics gfx, string text, XFont font, double maxWidth)
        {
            if (string.IsNullOrEmpty(text) || gfx.MeasureString(text, font).Width <= maxWidth)
            {
                return font;
            }

            var size = font.Size;
            while (size > 4.5)
            {
                size -= 0.4;
                var candidate = CreateFont(size, font.Style);
                if (gfx.MeasureString(text, candidate).Width <= maxWidth)
                {
                    return candidate;
                }
            }

            return CreateFont(4.5, font.Style);
        }

        private static string GetShiftStatusSymbol(ShiftState? state)
        {
            return state switch
            {
                ShiftState.Accepted => "\u25cb",
                ShiftState.WantToGiveAway => "\u25b3",
                ShiftState.NotAccepted => "",
                ShiftState.KeyHolder => "\u25cb",
                null or ShiftState.None => "\u00d7",
                _ => ""
            };
        }

        private static XBrush GetShiftBrush(ShiftSubmission? submission, ShiftPdfUser user)
        {
            if (submission == null || submission.ShiftStatus == ShiftState.None)
            {
                return XBrushes.Gray;
            }

            if (submission.ShiftStatus == ShiftState.WantToGiveAway)
            {
                return XBrushes.Orange;
            }

            if (submission.ShiftStatus == ShiftState.KeyHolder ||
                (submission.ShiftStatus == ShiftState.Accepted &&
                 (submission.UserShiftRole == UserShiftRole.KeyHolder ||
                  user.UserShiftRole == UserShiftRole.KeyHolder)))
            {
                return XBrushes.Red;
            }

            return XBrushes.Black;
        }

        private class ShiftPdfLayout
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double FixedWidth { get; set; }
            public double ShiftColumnWidth { get; set; }
            public double FirstShiftColumnLeft => Left + FixedWidth;
            public double UserRowsTop => Top + (HeaderRowHeight * 4);
        }

        private class ShiftColumnChunk
        {
            public int StartIndex { get; set; }
            public int EndIndex => StartIndex + Columns.Count;
            public List<ShiftTableColumn> Columns { get; set; } = new();
            public List<IndexedWorkloadCell> WorkloadCells { get; set; } = new();
        }

        private class IndexedWorkloadCell
        {
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public int RequiredCount { get; set; }
        }

        private class ShiftPdfUser
        {
            public string Id { get; set; } = "";
            public int CustomId { get; set; }
            public string Name { get; set; } = "";
            public UserShiftRole UserShiftRole { get; set; }
        }
    }
}
