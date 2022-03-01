using System.ComponentModel.DataAnnotations;

namespace DetAct.Data.Model
{
    public class Client
    {
        [Required]
        [StringLength(16, ErrorMessage = "Name is too long.")]
        public string Category { get; set; }

        [Required]
        public string Name { get; set; }

        public Client() => (Category, Name) = ("", "");
    }
}
