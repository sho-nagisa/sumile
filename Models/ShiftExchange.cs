using System;

namespace sumile.Models
{
    /// <summary>
    /// シフト交換のリクエストを管理するためのテーブル
    /// </summary>
    public class ShiftExchange
    {
        public int Id { get; set; }

        /// <summary>
        /// どのシフトアサインを交換に出すか (元々入っているシフト)
        /// FK: ShiftAssignment.Id
        /// </summary>
        public int ShiftAssignmentId { get; set; }
        public ShiftAssignment ShiftAssignment { get; set; }

        /// <summary>
        /// 募集を出したユーザー (ShiftAssignment.User) と同じはず
        /// いちいち検索しなくてもすぐ参照したいなら持っておく
        /// </summary>
        public string RequestedByUserId { get; set; }
        public ApplicationUser RequestedByUser { get; set; }

        /// <summary>
        /// 代わりに入ると承諾したユーザー
        /// </summary>
        public string AcceptedByUserId { get; set; }
        public ApplicationUser AcceptedByUser { get; set; }

        /// <summary>
        /// 交換の状態: "Pending", "Accepted", "Rejected", "Completed" etc.
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>作成日時</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>更新日時 (ステータス変更時に更新)</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
