using System;

namespace sumile.Models
{
    /// <summary>
    /// 1日につき、朝/夜などのシフト枠を表す
    /// </summary>
    public class Shift
    {
        public int Id { get; set; }

        /// <summary>シフトの日付 (例: 2025-03-10)</summary>
        public DateTime Date { get; set; }

        /// <summary>シフトの種類 ("Morning", "Night" など)</summary>
        public string ShiftType { get; set; }

        /// <summary>最大人数 (例: 6 or 7)</summary>
        public int MaxCapacity { get; set; } = 6;
    }
}
