using sumile.Models;

namespace sumile.Services
{
    /// <summary>
    /// ShiftState から表示用シンボル・CSS 等を決定する専用サービス
    /// 表示ロジックは必ずここを経由させる
    /// </summary>
    public class ShiftStatusDisplayService
    {
        /// <summary>
        /// 表示用文字（〇・△・×・空白）
        /// </summary>
        public string GetSymbol(ShiftState? status)
        {
            // null は None（未提出）として扱う
            if (!status.HasValue || status.Value == ShiftState.None)
                return "×";

            return status.Value switch
            {
                ShiftState.Accepted => "〇",
                ShiftState.WantToGiveAway => "△",
                ShiftState.NotAccepted => "",   // 空白
                ShiftState.KeyHolder => "〇",   // 表示文字は〇（色で区別）
                _ => ""
            };
        }

        /// <summary>
        /// CSS クラス（色・強調用）
        /// </summary>
        public string GetCssClass(ShiftState? status)
        {
            if (!status.HasValue || status.Value == ShiftState.None)
                return "shift-none";           // ×

            return status.Value switch
            {
                ShiftState.Accepted => "shift-accepted",
                ShiftState.NotAccepted => "shift-notaccepted",
                ShiftState.WantToGiveAway => "shift-want",
                ShiftState.KeyHolder => "shift-keyholder",
                _ => ""
            };
        }

        /// <summary>
        /// 表示用まとめ（View で使うならこれだけ呼べばよい）
        /// </summary>
        public ShiftDisplayResult GetDisplay(ShiftState? status)
        {
            return new ShiftDisplayResult
            {
                Symbol = GetSymbol(status),
                CssClass = GetCssClass(status)
            };
        }
    }

    /// <summary>
    /// 表示用 DTO
    /// </summary>
    public class ShiftDisplayResult
    {
        public string Symbol { get; set; } = "";
        public string CssClass { get; set; } = "";
    }
}
