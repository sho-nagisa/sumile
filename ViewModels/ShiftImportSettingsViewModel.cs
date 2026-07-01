using System.ComponentModel.DataAnnotations;

namespace sumile.ViewModels
{
    public class ShiftImportSettingsViewModel
    {
        public int CustomId { get; set; }

        public string Name { get; set; } = string.Empty;

        [Display(Name = "PDF上の検索名")]
        [StringLength(80, ErrorMessage = "PDF上の検索名は80文字以内で入力してください。")]
        public string? ShiftPdfSearchName { get; set; }

        [Display(Name = "予備のスタッフ行番号")]
        [Range(1, 200, ErrorMessage = "スタッフ行番号は1以上200以下で入力してください。")]
        public int? ShiftPdfStaffRowNumber { get; set; }

        [Display(Name = "ショートカット用キー")]
        public string? ShiftImportApiKey { get; set; }
    }
}
