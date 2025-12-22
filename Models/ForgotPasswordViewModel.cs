using System.ComponentModel.DataAnnotations;

namespace WebDienThoai.Models
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [Display(Name = "Tên ??ng nh?p")]
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email ?ã ??ng ký")]
        public string Email { get; set; }
    }
}
