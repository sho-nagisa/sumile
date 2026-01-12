using System;
using sumile.Models;

namespace sumile.Models
{
    /// <summary>
    /// 募集締切時点のシフト提出状態を保存するスナップショット
    /// ・締切時にのみ作成
    /// ・以降は一切更新しない
    /// ・差分比較の基準点として使用
    /// </summary>
    public class SubmitBackup
    {
        public int Id { get; set; }

        /// 募集期間ID
        public int RecruitmentPeriodId { get; set; }

        /// 対象ユーザーID
        public string UserId { get; set; }
        /// シフト日ID
        public int ShiftDayId { get; set; }
        /// シフトタイプ（上／敷）
        public ShiftType ShiftType { get; set; }
        /// シフト提出状態(〇×)
        public ShiftState ShiftStatus { get; set; }
        /// バックアップ取得日時
        public DateTime BackedUpAt { get; set; } = DateTime.UtcNow;
    }
}
