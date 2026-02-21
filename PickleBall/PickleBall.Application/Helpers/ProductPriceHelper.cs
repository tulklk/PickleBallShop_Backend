namespace PickleBall.Application.Helpers;

/// <summary>
/// Giá bán hiển thị: có Giá giảm (salePrice) thì dùng Giá giảm, không thì dùng Giá gốc (originalPrice).
/// </summary>
public static class ProductPriceHelper
{
    /// <summary>
    /// Tính giá bán hiệu lực: nếu salePrice &gt; 0 dùng salePrice (Giá giảm), ngược lại dùng originalPrice (Giá gốc).
    /// </summary>
    public static decimal GetEffectivePrice(decimal salePrice, decimal? originalPrice)
    {
        return salePrice > 0 ? salePrice : (originalPrice ?? 0);
    }

    /// <summary>
    /// Lưu giá vào entity: Price = giá bán hiệu lực, OriginalPrice = chỉ set khi đang giảm giá (để hiển thị gạch giá).
    /// </summary>
    public static void SetEffectivePrices(decimal salePrice, decimal? originalPrice, out decimal effectivePrice, out decimal? storedOriginalPrice)
    {
        effectivePrice = GetEffectivePrice(salePrice, originalPrice);
        storedOriginalPrice = salePrice > 0 && originalPrice.HasValue && originalPrice.Value > 0 ? originalPrice : null;
    }
}
