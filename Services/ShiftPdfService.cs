using sumile.Data;
using sumile.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Microsoft.EntityFrameworkCore;

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
            // ShiftDayとUserを含めて読み込む（RecruitmentPeriodIdはShiftDay経由で参照）
            var submissions = await _context.ShiftSubmissions
                .Include(s => s.User)
                .Include(s => s.ShiftDay)
                .Where(s => s.ShiftDay.RecruitmentPeriodId == periodId)
                .OrderBy(s => s.ShiftDay.Date)
                .ThenBy(s => s.User.Name)
                .ToListAsync();

            var document = new PdfDocument();
            var page = document.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;

            var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Meiryo UI", 10, XFontStyle.Regular);

            int y = 40;
            gfx.DrawString($"シフト表 (期間ID: {periodId})", new XFont("Meiryo UI", 16, XFontStyle.Bold), XBrushes.Black, new XPoint(40, y));
            y += 30;

            foreach (var shift in submissions)
            {
                var shiftInfo = $"{shift.ShiftDay.Date:yyyy/MM/dd} - {shift.User.Name} - {shift.ShiftType} - {shift.ShiftStatus}";
                gfx.DrawString(shiftInfo, font, XBrushes.Black, new XPoint(40, y));
                y += 20;

                if (y > page.Height - 50)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = 40;
                }
            }

            var folder = Path.Combine(_env.WebRootPath, "shift_pdfs");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var filePath = Path.Combine(folder, $"shift_{periodId}.pdf");
            using var fs = new FileStream(filePath, FileMode.Create);
            document.Save(fs);
        }
    }
}
