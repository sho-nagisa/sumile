using sumile.Models;

namespace sumile.ViewModels
{
    public class ExchangeIndexPageViewModel
    {
        public List<ShiftExchange> Exchanges { get; set; } = new();
        public string CurrentUserId { get; set; } = string.Empty;
        public string CurrentUserRole { get; set; } = "Normal";
        public bool IsAdmin { get; set; }
        public bool RelatedOnly { get; set; }
    }
}
