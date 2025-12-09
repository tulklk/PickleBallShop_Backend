# Prompt cho Frontend Developer

## Yêu cầu: Tích hợp URL SEO-Friendly với Backend

Backend đã được cập nhật để hỗ trợ **slug-based URLs**. Cần update frontend để sử dụng slug thay vì ID trong URL sản phẩm.

### API Endpoints

1. **Lấy sản phẩm theo slug (MỚI - SEO-friendly):**
   ```
   GET /api/products/slug/{slug}
   ```
   Ví dụ: `GET /api/products/slug/ao-polo-pickleball-performance`

2. **Lấy sản phẩm theo ID (Vẫn hoạt động - backward compatible):**
   ```
   GET /api/products/{id}
   ```

3. **Danh sách sản phẩm (đã có slug trong response):**
   ```
   GET /api/products?page=1&pageSize=20
   ```

### Response Structure

Product object bây giờ có thêm field `slug`:

```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "name": "Áo Polo Pickleball Performance",
  "slug": "ao-polo-pickleball-performance",  // ← MỚI
  "price": 450000,
  // ... các fields khác
}
```

### Tasks Cần Làm

1. ✅ **Update Product Detail Route:**
   - Thay đổi từ `/products/:id` → `/products/:slug`
   - Update API call từ `/api/products/{id}` → `/api/products/slug/{slug}`

2. ✅ **Update Product List Links:**
   - Thay tất cả links từ `product.id` → `product.slug`
   - Ví dụ: `<Link to={`/products/${product.slug}`}>` thay vì `product.id`

3. ✅ **SEO Optimization:**
   - Update canonical URLs với slug
   - Update meta tags (og:url) với slug
   - Update sitemap với slug URLs

4. ✅ **Error Handling:**
   - Handle 404 khi slug không tồn tại
   - Redirect hoặc show error message phù hợp

### Ví dụ Code (React/Next.js)

```typescript
// Product Detail Page
const { slug } = useParams();

useEffect(() => {
  fetch(`/api/products/slug/${slug}`)
    .then(res => res.json())
    .then(setProduct);
}, [slug]);

// Product List Card
<Link to={`/products/${product.slug}`}>
  <h3>{product.name}</h3>
</Link>
```

### Lưu ý

- ✅ Slug đã được backend tự động generate và có sẵn trong API response
- ✅ Không cần generate slug ở client-side
- ✅ Backward compatible: Vẫn hỗ trợ ID nếu cần
- ✅ Slug là unique và SEO-friendly (lowercase, không dấu, dùng dấu gạch ngang)

### Checklist

- [ ] Update routing (products/:slug)
- [ ] Update API calls (sử dụng slug endpoint)
- [ ] Update tất cả links trong product lists/cards
- [ ] Update canonical URLs
- [ ] Update meta tags
- [ ] Update sitemap
- [ ] Test với các slugs khác nhau
- [ ] Handle 404 errors

---

**Xem file `FRONTEND_SEO_URL_INTEGRATION.md` để có hướng dẫn chi tiết và ví dụ code cho các framework khác nhau (React, Vue, Angular, Next.js).**

