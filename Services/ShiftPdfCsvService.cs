using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace sumile.Services
{
    public class ShiftPdfCsvService
    {
        private static readonly Regex DateHeaderPattern = new(@"^\d{1,2}/\d{1,2}$", RegexOptions.Compiled);
        private static readonly Regex FullDatePattern = new(@"\b(?<year>\d{4})/(?<month>\d{1,2})/(?<day>\d{1,2})\b", RegexOptions.Compiled);

        public ShiftPdfCsvResult Convert(Stream pdfStream, ShiftPdfCsvOptions options)
        {
            using var document = PdfDocument.Open(pdfStream);
            if (document.NumberOfPages < 1)
            {
                throw new InvalidOperationException("PDFにページがありません。");
            }

            if (options.PageNumber < 1 || options.PageNumber > document.NumberOfPages)
            {
                throw new InvalidOperationException($"PDFは{document.NumberOfPages}ページです。ページ番号を確認してください。");
            }

            var page = document.GetPage(options.PageNumber);
            var rows = BuildRows(page.Letters);
            if (rows.Count == 0)
            {
                throw new InvalidOperationException("PDF内の文字を読み取れませんでした。画像だけのPDFはこの変換では対応していません。");
            }

            var dateRow = FindDateHeaderRow(rows);
            var year = DetectBaseYear(rows);
            var dates = BuildDates(dateRow.DateChunks, year);
            var shiftRow = FindShiftHeaderRow(rows, dateRow.Row, dates.Count * 2);
            var columns = BuildColumns(dates, shiftRow.LabelChunks);
            var staffRows = FindStaffRows(rows, shiftRow.Row, columns);

            var times = ShiftPdfCsvTimes.Parse(options);
            var targetRow = ResolveTargetRow(staffRows, columns, options);
            var events = BuildEvents(targetRow.Row, columns, times, options, targetRow.RowNumber, targetRow.StaffName).ToList();
            var csv = BuildCsv(events);

            return new ShiftPdfCsvResult(
                csv,
                events,
                staffRows.Count,
                targetRow.RowNumber,
                targetRow.StaffName,
                dates.First(),
                dates.Last());
        }

        private static ShiftPdfDateRow FindDateHeaderRow(List<TextRow> rows)
        {
            var candidate = rows
                .Select(row => new ShiftPdfDateRow(
                    row,
                    row.Chunks
                        .Where(chunk => DateHeaderPattern.IsMatch(chunk.Text))
                        .OrderBy(chunk => chunk.CenterX)
                        .ToList()))
                .Where(row => row.DateChunks.Count >= 2)
                .OrderByDescending(row => row.DateChunks.Count)
                .ThenByDescending(row => row.Row.CenterY)
                .FirstOrDefault();

            return candidate ?? throw new InvalidOperationException("日付行を読み取れませんでした。");
        }

        private static ShiftPdfShiftRow FindShiftHeaderRow(List<TextRow> rows, TextRow dateRow, int expectedColumns)
        {
            var minLabels = Math.Max(2, expectedColumns - 2);
            var candidate = rows
                .Where(row => row.CenterY < dateRow.CenterY)
                .Select(row => new ShiftPdfShiftRow(
                    row,
                    row.Chunks
                        .Where(chunk => IsShiftLabel(chunk.Text))
                        .OrderBy(chunk => chunk.CenterX)
                        .ToList()))
                .Where(row => row.LabelChunks.Count >= minLabels)
                .OrderByDescending(row => row.Row.CenterY)
                .FirstOrDefault();

            if (candidate == null)
            {
                throw new InvalidOperationException("上・敷の見出し行を読み取れませんでした。");
            }

            if (candidate.LabelChunks.Count > expectedColumns)
            {
                candidate = candidate with
                {
                    LabelChunks = candidate.LabelChunks
                        .OrderBy(chunk => chunk.CenterX)
                        .Take(expectedColumns)
                        .ToList()
                };
            }

            if (candidate.LabelChunks.Count != expectedColumns)
            {
                throw new InvalidOperationException("日付と上・敷の列数が一致しませんでした。");
            }

            return candidate;
        }

        private static List<ShiftPdfColumn> BuildColumns(
            List<DateOnly> dates,
            List<TextChunk> labelChunks)
        {
            var columns = new List<ShiftPdfColumn>();
            for (var i = 0; i < labelChunks.Count; i++)
            {
                var label = labelChunks[i].Text;
                var date = dates[Math.Min(i / 2, dates.Count - 1)];
                columns.Add(new ShiftPdfColumn(date, label, labelChunks[i].CenterX));
            }

            for (var i = 0; i < columns.Count; i++)
            {
                // Adjacent header positions are used as implicit cell borders because the PDF has no table model.
                var left = i == 0
                    ? columns[i].CenterX - ((columns[i + 1].CenterX - columns[i].CenterX) / 2)
                    : (columns[i - 1].CenterX + columns[i].CenterX) / 2;

                var right = i == columns.Count - 1
                    ? columns[i].CenterX + ((columns[i].CenterX - columns[i - 1].CenterX) / 2)
                    : (columns[i].CenterX + columns[i + 1].CenterX) / 2;

                columns[i] = columns[i] with { Left = left, Right = right };
            }

            return columns;
        }

        private static List<TextRow> FindStaffRows(
            List<TextRow> rows,
            TextRow shiftRow,
            List<ShiftPdfColumn> columns)
        {
            var minimumSymbols = Math.Max(3, columns.Count / 4);

            return rows
                .Where(row => row.CenterY < shiftRow.CenterY)
                .Select(row => new
                {
                    Row = row,
                    SymbolCount = ExtractCellStatuses(row, columns)
                        .Count(status => IsKnownStatus(status))
                })
                .Where(row => row.SymbolCount >= minimumSymbols)
                .OrderByDescending(row => row.Row.CenterY)
                .Select(row => row.Row)
                .ToList();
        }

        private static ShiftPdfTargetRow ResolveTargetRow(
            List<TextRow> staffRows,
            List<ShiftPdfColumn> columns,
            ShiftPdfCsvOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.StaffSearchName))
            {
                var searchMatch = FindStaffRowBySearchName(staffRows, columns, options.StaffSearchName);
                if (searchMatch != null)
                {
                    return searchMatch;
                }

                if (!options.StaffRowNumber.HasValue)
                {
                    throw new InvalidOperationException(
                        $"PDF上の検索名「{options.StaffSearchName}」に一致するスタッフ行が見つかりませんでした。");
                }
            }

            if (!options.StaffRowNumber.HasValue || options.StaffRowNumber.Value < 1)
            {
                throw new InvalidOperationException("スタッフ行番号を確認してください。");
            }

            if (options.StaffRowNumber.Value > staffRows.Count)
            {
                throw new InvalidOperationException(
                    $"読み取れたスタッフ行は{staffRows.Count}行です。スタッフ行番号を確認してください。");
            }

            var targetRow = staffRows[options.StaffRowNumber.Value - 1];
            return new ShiftPdfTargetRow(
                targetRow,
                options.StaffRowNumber.Value,
                ExtractStaffName(targetRow, columns));
        }

        private static ShiftPdfTargetRow? FindStaffRowBySearchName(
            List<TextRow> staffRows,
            List<ShiftPdfColumn> columns,
            string searchName)
        {
            var normalizedSearchName = NormalizeStaffName(searchName);
            if (string.IsNullOrWhiteSpace(normalizedSearchName))
            {
                return null;
            }

            var matches = staffRows
                .Select((row, index) => new
                {
                    Row = row,
                    RowNumber = index + 1,
                    StaffName = ExtractStaffName(row, columns)
                })
                .Where(item => NormalizeStaffName(item.StaffName) == normalizedSearchName)
                .ToList();

            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"PDF上の検索名「{searchName}」に一致するスタッフ行が複数あります。フルネームで登録してください。");
            }

            var match = matches.SingleOrDefault();
            return match == null
                ? null
                : new ShiftPdfTargetRow(match.Row, match.RowNumber, match.StaffName);
        }

        private static string ExtractStaffName(TextRow row, List<ShiftPdfColumn> columns)
        {
            var nameRight = columns.Min(column => column.Left);
            var rawName = string.Concat(row.Glyphs
                .Where(glyph => glyph.CenterX < nameRight)
                .OrderBy(glyph => glyph.Left)
                .Select(glyph => glyph.Text));

            return CleanStaffName(rawName);
        }

        private static string CleanStaffName(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in value)
            {
                if (char.IsWhiteSpace(character) || IsStaffNameIgnoredCharacter(character))
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        private static string NormalizeStaffName(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in value.Normalize(NormalizationForm.FormKC))
            {
                if (char.IsWhiteSpace(character) || IsStaffNameIgnoredCharacter(character))
                {
                    continue;
                }

                builder.Append(NormalizeStaffNameCharacter(character));
            }

            return builder.ToString();
        }

        private static char NormalizeStaffNameCharacter(char character)
        {
            return character switch
            {
                '髙' => '高',
                '﨑' => '崎',
                _ => character
            };
        }

        private static bool IsStaffNameIgnoredCharacter(char character)
        {
            return character is '★' or '☆' or '○' or '〇' or '◯' or '●'
                or '△' or '▲' or '×' or '✕' or '✖';
        }

        private static IEnumerable<ShiftPdfCsvEvent> BuildEvents(
            TextRow row,
            List<ShiftPdfColumn> columns,
            ShiftPdfCsvTimes times,
            ShiftPdfCsvOptions options,
            int staffRowNumber,
            string staffName)
        {
            var statuses = ExtractCellStatuses(row, columns);

            for (var i = 0; i < columns.Count; i++)
            {
                var status = statuses[i];
                if (!IsScheduledStatus(status, options.IncludeTriangle))
                {
                    continue;
                }

                var column = columns[i];
                var shiftName = column.Label == "上" ? "上げ" : "敷き";
                var start = column.Label == "上" ? times.MorningStart : times.NightStart;
                var end = column.Label == "上" ? times.MorningEnd : times.NightEnd;
                var endDate = end <= start ? column.Date.AddDays(1) : column.Date;
                var subject = string.IsNullOrWhiteSpace(options.SubjectPrefix)
                    ? shiftName
                    : $"{options.SubjectPrefix.Trim()} {shiftName}";

                yield return new ShiftPdfCsvEvent(
                    subject,
                    column.Date,
                    start,
                    endDate,
                    end,
                    column.Label,
                    shiftName,
                    status,
                    BuildDescription(staffRowNumber, shiftName, status));
            }
        }

        private static string BuildDescription(
            int staffRowNumber,
            string shiftName,
            string status)
        {
            return $"行番号: {staffRowNumber}, シフト: {shiftName}, 記号: {status}";
        }

        private static List<string> ExtractCellStatuses(TextRow row, List<ShiftPdfColumn> columns)
        {
            return columns
                .Select(column =>
                {
                    var text = string.Concat(row.Glyphs
                        .Where(glyph => glyph.CenterX >= column.Left && glyph.CenterX < column.Right)
                        .OrderBy(glyph => glyph.Left)
                        .Select(glyph => glyph.Text));

                    return NormalizeStatus(text);
                })
                .ToList();
        }

        private static string BuildCsv(IReadOnlyList<ShiftPdfCsvEvent> events)
        {
            var builder = new StringBuilder();
            AppendCsvLine(builder, new[]
            {
                "Subject",
                "Start Date",
                "Start Time",
                "End Date",
                "End Time",
                "All Day Event",
                "Description"
            });

            foreach (var item in events)
            {
                AppendCsvLine(builder, new[]
                {
                    item.Subject,
                    item.StartDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                    item.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    item.EndDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                    item.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    "FALSE",
                    item.Description
                });
            }

            return builder.ToString();
        }

        private static void AppendCsvLine(StringBuilder builder, IEnumerable<string> values)
        {
            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private static List<DateOnly> BuildDates(List<TextChunk> dateChunks, int baseYear)
        {
            var dates = new List<DateOnly>();
            var year = baseYear;
            int? previousMonth = null;

            foreach (var chunk in dateChunks.OrderBy(chunk => chunk.CenterX))
            {
                var parts = chunk.Text.Split('/');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], out var month) ||
                    !int.TryParse(parts[1], out var day))
                {
                    continue;
                }

                if (previousMonth.HasValue && month < previousMonth.Value)
                {
                    year++;
                }

                dates.Add(new DateOnly(year, month, day));
                previousMonth = month;
            }

            if (dates.Count == 0)
            {
                throw new InvalidOperationException("日付を読み取れませんでした。");
            }

            return dates;
        }

        private static int DetectBaseYear(List<TextRow> rows)
        {
            var text = string.Join(" ", rows.SelectMany(row => row.Chunks.Select(chunk => chunk.Text)));
            var match = FullDatePattern.Match(text);
            return match.Success
                ? int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture)
                : DateTime.Today.Year;
        }

        private static List<TextRow> BuildRows(IReadOnlyList<Letter> letters)
        {
            var glyphs = letters
                .Where(letter => !string.IsNullOrWhiteSpace(letter.Value))
                .Select(ToGlyph)
                .OrderByDescending(glyph => glyph.CenterY)
                .ToList();

            var rows = new List<TextRow>();
            foreach (var glyph in glyphs)
            {
                var row = rows.LastOrDefault();
                // Letters with nearly the same Y coordinate are treated as one visual table row.
                if (row == null || Math.Abs(row.CenterY - glyph.CenterY) > 4.5)
                {
                    rows.Add(new TextRow(new List<PdfGlyph> { glyph }));
                    continue;
                }

                row.Glyphs.Add(glyph);
                row.CenterY = row.Glyphs.Average(item => item.CenterY);
            }

            foreach (var row in rows)
            {
                row.Glyphs = row.Glyphs
                    .OrderBy(glyph => glyph.Left)
                    .ToList();
                row.Chunks = BuildChunks(row.Glyphs);
            }

            return rows;
        }

        private static PdfGlyph ToGlyph(Letter letter)
        {
            var box = letter.BoundingBox;
            var left = System.Convert.ToDouble(box.Left, CultureInfo.InvariantCulture);
            var right = System.Convert.ToDouble(box.Right, CultureInfo.InvariantCulture);
            var top = System.Convert.ToDouble(box.Top, CultureInfo.InvariantCulture);
            var bottom = System.Convert.ToDouble(box.Bottom, CultureInfo.InvariantCulture);

            return new PdfGlyph(letter.Value, left, right, top, bottom);
        }

        private static List<TextChunk> BuildChunks(List<PdfGlyph> glyphs)
        {
            if (glyphs.Count == 0)
            {
                return new List<TextChunk>();
            }

            var chunks = new List<List<PdfGlyph>>();
            var current = new List<PdfGlyph> { glyphs[0] };
            var medianWidth = Median(glyphs.Select(glyph => glyph.Width).Where(width => width > 0).ToList());
            var maxGap = Math.Max(2.0, medianWidth * 0.85);

            foreach (var glyph in glyphs.Skip(1))
            {
                var gap = glyph.Left - current.Max(item => item.Right);
                if (gap > maxGap)
                {
                    chunks.Add(current);
                    current = new List<PdfGlyph>();
                }

                current.Add(glyph);
            }

            chunks.Add(current);

            return chunks
                .Select(chunk => new TextChunk(
                    string.Concat(chunk.OrderBy(glyph => glyph.Left).Select(glyph => glyph.Text)).Trim(),
                    chunk.Min(glyph => glyph.Left),
                    chunk.Max(glyph => glyph.Right),
                    chunk.Average(glyph => glyph.CenterX)))
                .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Text))
                .ToList();
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0)
            {
                return 3.0;
            }

            values.Sort();
            var middle = values.Count / 2;
            return values.Count % 2 == 0
                ? (values[middle - 1] + values[middle]) / 2
                : values[middle];
        }

        private static bool IsShiftLabel(string text)
        {
            return text is "上" or "敷";
        }

        private static string NormalizeStatus(string text)
        {
            if (text.Contains('○') || text.Contains('〇') || text.Contains('◯') || text.Contains('●'))
            {
                return "○";
            }

            if (text.Contains('△') || text.Contains('▲'))
            {
                return "△";
            }

            if (text.Contains('×') || text.Contains('✕') || text.Contains('✖') || text.Contains('X') || text.Contains('x'))
            {
                return "×";
            }

            return string.IsNullOrWhiteSpace(text) ? "" : text.Trim();
        }

        private static bool IsKnownStatus(string status)
        {
            return status is "○" or "△" or "×";
        }

        private static bool IsScheduledStatus(string status, bool includeTriangle)
        {
            return status == "○" || (includeTriangle && status == "△");
        }

        private sealed record ShiftPdfDateRow(TextRow Row, List<TextChunk> DateChunks);

        private sealed record ShiftPdfShiftRow(TextRow Row, List<TextChunk> LabelChunks);

        private sealed record ShiftPdfTargetRow(TextRow Row, int RowNumber, string StaffName);

        private sealed record TextChunk(string Text, double Left, double Right, double CenterX);

        private sealed record PdfGlyph(string Text, double Left, double Right, double Top, double Bottom)
        {
            public double CenterX => (Left + Right) / 2;
            public double CenterY => (Top + Bottom) / 2;
            public double Width => Right - Left;
        }

        private sealed class TextRow
        {
            public TextRow(List<PdfGlyph> glyphs)
            {
                Glyphs = glyphs;
                CenterY = glyphs.Average(glyph => glyph.CenterY);
            }

            public double CenterY { get; set; }
            public List<PdfGlyph> Glyphs { get; set; }
            public List<TextChunk> Chunks { get; set; } = new();
        }

        private sealed record ShiftPdfColumn(DateOnly Date, string Label, double CenterX)
        {
            public double Left { get; init; }
            public double Right { get; init; }
        }

        private sealed record ShiftPdfCsvTimes(
            TimeOnly MorningStart,
            TimeOnly MorningEnd,
            TimeOnly NightStart,
            TimeOnly NightEnd)
        {
            public static ShiftPdfCsvTimes Parse(ShiftPdfCsvOptions options)
            {
                return new ShiftPdfCsvTimes(
                    ParseTime(options.MorningStartTime, "上げ開始"),
                    ParseTime(options.MorningEndTime, "上げ終了"),
                    ParseTime(options.NightStartTime, "敷き開始"),
                    ParseTime(options.NightEndTime, "敷き終了"));
            }

            private static TimeOnly ParseTime(string value, string label)
            {
                if (TimeOnly.TryParseExact(
                        value,
                        new[] { "H:mm", "HH:mm" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var time))
                {
                    return time;
                }

                throw new InvalidOperationException($"{label}はHH:mm形式で入力してください。");
            }
        }
    }

    public sealed record ShiftPdfCsvOptions(
        int PageNumber,
        int? StaffRowNumber,
        string? StaffSearchName,
        string SubjectPrefix,
        string MorningStartTime,
        string MorningEndTime,
        string NightStartTime,
        string NightEndTime,
        bool IncludeTriangle);

    public sealed record ShiftPdfCsvResult(
        string Csv,
        IReadOnlyList<ShiftPdfCsvEvent> Events,
        int DetectedStaffRows,
        int SelectedStaffRowNumber,
        string SelectedStaffName,
        DateOnly RangeStartDate,
        DateOnly RangeEndDate);

    public sealed record ShiftPdfCsvEvent(
        string Subject,
        DateOnly StartDate,
        TimeOnly StartTime,
        DateOnly EndDate,
        TimeOnly EndTime,
        string ShiftLabel,
        string ShiftName,
        string Status,
        string Description);
}
