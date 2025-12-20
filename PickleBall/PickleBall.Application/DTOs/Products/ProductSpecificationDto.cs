namespace PickleBall.Application.DTOs.Products;

public class ProductSpecificationDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public class CreateProductSpecificationRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
}

public class UpdateProductSpecificationRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
}

public class BulkUpdateProductSpecificationsRequest
{
    public List<CreateProductSpecificationRequest> Specifications { get; set; } = new();
}
