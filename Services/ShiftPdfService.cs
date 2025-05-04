using sumile.Data;
using sumile.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Microsoft.EntityFrameworkCore;
using System.IO;
using PdfSharpCore;

namespace sumile.Services
{
    public class ShiftPdfService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ShiftPdfService(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task GenerateShiftPdfAsync(int periodId)
        {
            // 期間IDに紐づくシフト提出情報を取得、日付→シフト種別→ユーザー名の順でソート
            var submissions = await _context.ShiftSubmissions
                .Include(s => s.User)
                .Include(s => s.ShiftDay)
                .Where(s => s.ShiftDay.RecruitmentPeriodId == periodId)
                .OrderBy(s => s.ShiftDay.Date)
                .ThenBy(s => s.ShiftType)
                .ThenBy(s => s.User.Name)
                .ToListAsync();

            var document = new PdfDocument();
            var page = document.AddPage();
            page.Size = PageSize.A4;

            var gfx = XGraphics.FromPdfPage(page);
            // カスタムフォントリゾルバをProgram.csで設定している前提
            var headerFont = new XFont("NotoSansJP", 16, XFontStyle.Bold, new XPdfFontOptions(PdfFontEncoding.Unicode));
            var regularFont = new XFont("NotoSansJP", 10, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));

            int y = 40;
            // ヘッダーを描画
            gfx.DrawString($"シフト表（期間ID: {periodId}）", headerFont, XBrushes.Black, new XPoint(40, y));
            y += 30;

            // 各シフトを行単位で描画
            foreach (var shift in submissions)
            {
                var shiftLine = $"{shift.ShiftDay.Date:yyyy/MM/dd}（{GetShiftTypeLabel(shift.ShiftType)}） - {shift.User.Name} - {GetShiftStatusLabel(shift.ShiftStatus)}";
                gfx.DrawString(shiftLine, regularFont, XBrushes.Black, new XPoint(40, y));
                y += 20;

                // ページ下部に近づいたら改ページ
                if (y > page.Height - 50)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = 40;
                }
            }

            // PDF保存用フォルダ準備
            var folder = Path.Combine(_env.WebRootPath, "shift_pdfs");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var filePath = Path.Combine(folder, $"shift_{periodId}.pdf");
            using var fs = new FileStream(filePath, FileMode.Create);
            document.Save(fs);
        }

        // シフト種別を日本語ラベルに変換
        private static string GetShiftTypeLabel(ShiftType type) => type switch
        {
            ShiftType.Morning => "朝",
            ShiftType.Night => "夜",
            _ => "？"
        };

        // シフトステータスを記号に変換
        private static string GetShiftStatusLabel(ShiftState state) => state switch
        {
            ShiftState.Accepted => "〇",
            ShiftState.WantToGiveAway => "△",
            ShiftState.NotAccepted => "×",
            _ => "―"
        };
    }
}
