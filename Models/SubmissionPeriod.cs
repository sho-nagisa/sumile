using System;
using System.ComponentModel.DataAnnotations;

namespace sumile.Models
{
    public class SubmissionPeriod
    {
        public int Id { get; set; }

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }
    }
}
