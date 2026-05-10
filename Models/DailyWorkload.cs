using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sumile.Models
{
    public class DailyWorkload
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ShiftDayId { get; set; }

        [ForeignKey("ShiftDayId")]
        public ShiftDay ShiftDay { get; set; } = null!;

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
}
