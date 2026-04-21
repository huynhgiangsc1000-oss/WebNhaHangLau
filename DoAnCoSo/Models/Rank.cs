using System.ComponentModel.DataAnnotations;

namespace DoAnCoSo.Models
{
    public class Rank
    {
        [Key]
        public int RankId { get; set; }
        public string RankName { get; set; }
        public decimal DiscountPercent { get; set; }
        public int RequiredPoints { get; set; }
    }
}
