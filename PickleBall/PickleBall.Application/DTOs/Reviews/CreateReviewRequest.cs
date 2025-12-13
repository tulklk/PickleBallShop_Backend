using System.ComponentModel.DataAnnotations;

namespace PickleBall.Application.DTOs.Reviews;

public class CreateReviewRequest
{
    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(256, ErrorMessage = "Họ tên không được vượt quá 256 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
    [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(256, ErrorMessage = "Email không được vượt quá 256 ký tự")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Đánh giá sao là bắt buộc")]
    [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao")]
    public int Rating { get; set; }

    [StringLength(2000, ErrorMessage = "Bình luận không được vượt quá 2000 ký tự")]
    [MinLength(10, ErrorMessage = "Bình luận phải có ít nhất 10 ký tự")]
    public string? Comment { get; set; }

    [Url(ErrorMessage = "URL ảnh không hợp lệ")]
    [StringLength(1000, ErrorMessage = "URL ảnh không được vượt quá 1000 ký tự")]
    public string? ReviewImageUrl { get; set; }
}

