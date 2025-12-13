using System.ComponentModel.DataAnnotations;

namespace PickleBall.Application.DTOs.News;

public class CreateNewsRequest
{
    [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
    [StringLength(500, ErrorMessage = "Tiêu đề không được vượt quá 500 ký tự")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nội dung là bắt buộc")]
    [StringLength(50000, MinimumLength = 100, ErrorMessage = "Nội dung phải từ 100 đến 50000 ký tự")]
    public string Content { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Tóm tắt không được vượt quá 1000 ký tự")]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "Ảnh đại diện là bắt buộc")]
    [Url(ErrorMessage = "URL ảnh không hợp lệ")]
    [StringLength(1000, ErrorMessage = "URL ảnh không được vượt quá 1000 ký tự")]
    public string ThumbnailUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Danh mục là bắt buộc")]
    [StringLength(100, ErrorMessage = "Danh mục không được vượt quá 100 ký tự")]
    public string Category { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Tags không được vượt quá 500 ký tự")]
    public string? Tags { get; set; }

    public bool IsPublished { get; set; } = false;
}

