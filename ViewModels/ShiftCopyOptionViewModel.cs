namespace sumile.ViewModels
{
    public class ShiftCopyOptionViewModel
    {
        public List<ShiftCopyCellViewModel> Cells { get; set; } = new();
        public string? SourceLabel { get; set; }
    }

    public class ShiftCopyCellViewModel
    {
        public string Date { get; set; } = string.Empty;
        public string ShiftType { get; set; } = string.Empty;
        public string ShiftSymbol { get; set; } = string.Empty;
    }
}
