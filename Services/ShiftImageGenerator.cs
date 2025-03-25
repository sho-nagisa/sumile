using SkiaSharp;
using sumile.Models;  // ShiftSubmissionなどを使うなら
using System;
using System.Collections.Generic;

namespace sumile.Services
{
    /// <summary>
    /// シフト情報をもとに画像（PNGバイナリ）を生成するクラス
    /// </summary>
    public static class ShiftImageGenerator
    {
        /// <summary>
        /// シフト情報からPNG画像を生成し、バイト配列で返す
        /// </summary>
        /// <param name="submissions">シフト提出情報</param>
        /// <param name="dates">表示したい日付リスト</param>
        /// <returns>PNG形式のバイト配列</returns>
        public static byte[] GenerateShiftImage(
            List<ShiftSubmission> submissions,
            List<DateTime> dates)
        {
            // ---------------------------
            // 1) 画像サイズを決める
            // ---------------------------
            // ※実際のレイアウトや書きたい情報によって変えてください
            int width = 800;
            int height = 600;

            // SkiaSharp でキャンバスを準備
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;

            // 背景色を塗る
            canvas.Clear(SKColors.White);

            // テキスト描画用の設定
            using var paint = new SKPaint
            {
                TextSize = 24.0f,
                IsAntialias = true,
                Color = SKColors.Black,
                Typeface = SKTypeface.Default
            };

            // ---------------------------
            // 2) テキストやテーブルなどを自由に描画
            // ---------------------------
            // 例: シフトの件数を表示
            canvas.DrawText($"シフト提出数: {submissions.Count}", 30, 50, paint);

            // (本当は dates の一覧や「朝/夜」などを行列にして描画する... ここはお好みで)
            // 例えば、各日付ごとに何件あるかなど簡単に表示する例↓
            int y = 90;
            foreach (var date in dates)
            {
                int countThisDay = submissions.FindAll(s => s.Date.Date == date.Date).Count;
                var text = $"{date:yyyy-MM-dd} のシフト: {countThisDay}件";
                canvas.DrawText(text, 30, y, paint);
                y += 30;
            }

            // ---------------------------
            // 3) PNGにエンコードし、バイト配列を返す
            // ---------------------------
            using var snapshot = surface.Snapshot();
            using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);

            return data.ToArray();
        }
    }
}
