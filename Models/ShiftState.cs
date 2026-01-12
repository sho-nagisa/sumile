namespace sumile.Models
{
    public enum ShiftState
    {
        None = 0,             // × = シフト自体が存在しない（未提出）
        Accepted = 1,         // 〇 = 採用
        NotAccepted = 2,      // （空白）= 提出されたが未採用
        WantToGiveAway = 3,    // △ = 譲りたい
        KeyHolder = 4        // 赤丸 = キーホルダー
    }
}
