using System.ComponentModel.DataAnnotations;

namespace WebDienThoai.Models
{
    public class ResetPasswordWithOtpViewModel
    {
        [Required]
        public string UserName { get; set; }

        [Required]
        [Display(Name = "Mã xác th?c")]
        [StringLength(10, ErrorMessage = "OTP không h?p l?" )]
        public string Otp { get; set; }

        [Required]
        [Display(Name = "M?t kh?u m?i")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "{0} ph?i có ít nh?t {2} ký t?.", MinimumLength = 6)]
        public string NewPassword { get; set; }

        [Required]
        [Display(Name = "Nh?p l?i m?t kh?u m?i")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "M?t kh?u xác nh?n không kh?p.")]
        public string ConfirmNewPassword { get; set; }
    }
}
