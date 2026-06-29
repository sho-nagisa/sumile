using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace sumile.ViewModels
{
    public class ShiftPdfImportViewModel
    {
        [Display(Name = "PDFファイル")]
        public IFormFile? PdfFile { get; set; }

        [Display(Name = "ページ番号")]
        [Range(1, 20, ErrorMessage = "ページ番号は1以上20以下で入力してください。")]
        public int PageNumber { get; set; } = 1;

        [Display(Name = "スタッフ行番号")]
        [Range(1, 200, ErrorMessage = "スタッフ行番号は1以上200以下で入力してください。")]
        public int StaffRowNumber { get; set; } = 1;

        [Display(Name = "件名の先頭")]
        [StringLength(40, ErrorMessage = "件名の先頭は40文字以内で入力してください。")]
        public string SubjectPrefix { get; set; } = "ふなや";

        [Display(Name = "上げ開始")]
        [RegularExpression(@"^\d{1,2}:\d{2}$", ErrorMessage = "時刻はHH:mm形式で入力してください。")]
        public string MorningStartTime { get; set; } = "07:00";

        [Display(Name = "上げ終了")]
        [RegularExpression(@"^\d{1,2}:\d{2}$", ErrorMessage = "時刻はHH:mm形式で入力してください。")]
        public string MorningEndTime { get; set; } = "08:00";

        [Display(Name = "敷き開始")]
        [RegularExpression(@"^\d{1,2}:\d{2}$", ErrorMessage = "時刻はHH:mm形式で入力してください。")]
        public string NightStartTime { get; set; } = "18:30";

        [Display(Name = "敷き終了")]
        [RegularExpression(@"^\d{1,2}:\d{2}$", ErrorMessage = "時刻はHH:mm形式で入力してください。")]
        public string NightEndTime { get; set; } = "19:30";

        [Display(Name = "△も予定に含める")]
        public bool IncludeTriangle { get; set; } = true;
    }
}
