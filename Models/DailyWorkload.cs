using sumile.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

// Models/DailyWorkload.cs
public class DailyWorkload
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ShiftDayId { get; set; }

    [ForeignKey("ShiftDayId")]
    public ShiftDay ShiftDay { get; set; }

    // 現場で入力する「必要枚数」。人数ではない。
    [Required]
    public int RequiredCount { get; set; }

    // RequiredCount から算出した、その日に必要な人数。
    public int RequiredWorkers { get; set; }

    public static int CalculateRequiredWorkers(int count)
    {
        if (count <= 40) return 2;
        else if (count <= 80) return 4;
        else return 6;
    }

    public static int CalculateRequiredPeople(int count)
    {
        return CalculateRequiredWorkers(count);
    }
}
