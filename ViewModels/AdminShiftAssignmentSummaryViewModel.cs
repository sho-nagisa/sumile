namespace sumile.ViewModels
{
    public class AdminShiftAssignmentSummaryViewModel
    {
        public int ShiftCellCount { get; set; }
        public int RequiredWorkerTotal { get; set; }
        public int AssignedTotal { get; set; }
        public int KeyHolderAssignedTotal { get; set; }
        public int WorkerShortageCellCount { get; set; }
        public int WorkerShortageSlotCount { get; set; }
        public int KeyHolderShortageCellCount { get; set; }
        public int KeyHolderShortageSlotCount { get; set; }
        public int OverAssignedSlotCount { get; set; }
    }
}
