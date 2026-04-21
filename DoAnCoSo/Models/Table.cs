using System.ComponentModel.DataAnnotations;

namespace DoAnCoSo.Models
{
    public class Table
    {
        [Key]
        public int TableId { get; set; }
        public string TableName { get; set; }
        public string Status { get; set; } = "Empty";
        public string? QrCode { get; set; }
    }
}
