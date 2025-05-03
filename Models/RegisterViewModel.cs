using System.ComponentModel.DataAnnotations;

namespace sumile.Models
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "名前")]
        public string Name { get; set; }

        public int CustomId { get; set; } // 参照はないが自動割当のため消したらだめ

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "パスワード")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "パスワード確認")]
        [Compare("Password", ErrorMessage = "パスワードが一致しません。")]
        public string ConfirmPassword { get; set; }
    }
}
