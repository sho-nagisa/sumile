using sumile.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

// Models/DailyWorkload.cs
public class DailyWorkload
{
    [Key]
    public int Id { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    public int RequiredCount { get; set; }

    public int RequiredWorkers { get; set; }

    public int RecruitmentPeriodId { get; set; }

    [ForeignKey("RecruitmentPeriodId")]
    public RecruitmentPeriod RecruitmentPeriod { get; set; }

    // ✅ ここが static メソッドになっているか確認
    public static int CalculateRequiredPeople(int count)
    {
        if (count <= 40) return 2;
        else if (count <= 80) return 4;
        else return 6;
    }
}
