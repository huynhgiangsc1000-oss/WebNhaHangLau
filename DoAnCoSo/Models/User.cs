using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnCoSo.Models
{
    public class User : IdentityUser<int>
    {
        [Required]
        public string FullName { get; set; }

        public int Points { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Liên kết hạng thành viên
        public int? RankId { get; set; }
        [ForeignKey("RankId")]
        public virtual Rank? Rank { get; set; }
    }
}