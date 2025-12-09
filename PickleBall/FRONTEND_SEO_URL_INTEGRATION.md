# Hướng dẫn Tích hợp URL SEO-Friendly cho Frontend

## Tổng quan

Backend đã được cập nhật để hỗ trợ **slug-based URLs** cho sản phẩm, giúp URL thân thiện với SEO hơn.

## API Endpoints

### 1. Lấy sản phẩm theo Slug (Khuyến nghị - SEO-friendly)

```http
GET /api/products/slug/{slug}
```

**Ví dụ:**
```http
GET /api/products/slug/ao-polo-pickleball-performance
```

**Response:**
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "name": "Áo Polo Pickleball Performance",
  "slug": "ao-polo-pickleball-performance",
  "description": "...",
  "price": 450000,
  "originalPrice": 500000,
  // ... các fields khác
}
```

### 2. Lấy sản phẩm theo ID (Backward compatible)

```http
GET /api/products/{id}
```

**Ví dụ:**
```http
GET /api/products/123e4567-e89b-12d3-a456-426614174000
```

### 3. Danh sách sản phẩm (đã có sẵn slug)

```http
GET /api/products?page=1&pageSize=20
```

**Response:**
```json
{
  "items": [
    {
      "id": "...",
      "name": "Áo Polo Pickleball Performance",
      "slug": "ao-polo-pickleball-performance",
      "price": 450000,
      // ... các fields khác
    }
  ],
  "totalCount": 100,
  "page": 1,
  "pageSize": 20
}
```

## Cấu trúc Slug

Slug được tự động generate từ tên sản phẩm với các quy tắc:
- Bỏ dấu tiếng Việt: `Áo` → `ao`
- Lowercase: `Polo` → `polo`
- Thay space bằng dấu gạch ngang: `Polo Pickleball` → `polo-pickleball`
- Loại bỏ ký tự đặc biệt
- Unique: Nếu trùng sẽ tự động thêm số (`-1`, `-2`, ...)

**Ví dụ:**
- Tên: `"Áo Polo Pickleball Performance"`
- Slug: `"ao-polo-pickleball-performance"`

## Implementation Guide

### 1. React/Next.js - Product Detail Page

#### Cách 1: Sử dụng slug trong URL (Khuyến nghị)

```typescript
// pages/products/[slug].tsx (Next.js)
// hoặc routes/products/:slug (React Router)

import { useRouter } from 'next/router';
import { useEffect, useState } from 'react';

interface Product {
  id: string;
  name: string;
  slug: string;
  price: number;
  // ... other fields
}

export default function ProductDetailPage() {
  const router = useRouter();
  const { slug } = router.query;
  const [product, setProduct] = useState<Product | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!slug) return;

    fetch(`/api/products/slug/${slug}`)
      .then(res => res.json())
      .then(data => {
        setProduct(data);
        setLoading(false);
      })
      .catch(error => {
        console.error('Error fetching product:', error);
        setLoading(false);
      });
  }, [slug]);

  if (loading) return <div>Loading...</div>;
  if (!product) return <div>Product not found</div>;

  return (
    <div>
      <h1>{product.name}</h1>
      <p>Price: {product.price.toLocaleString('vi-VN')} ₫</p>
      {/* Product details */}
    </div>
  );
}
```

#### Cách 2: Hỗ trợ cả ID và Slug (Backward compatible)

```typescript
// pages/products/[idOrSlug].tsx

import { useRouter } from 'next/router';
import { useEffect, useState } from 'react';

export default function ProductDetailPage() {
  const router = useRouter();
  const { idOrSlug } = router.query;
  const [product, setProduct] = useState<Product | null>(null);

  useEffect(() => {
    if (!idOrSlug) return;

    // Kiểm tra xem có phải GUID không
    const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(idOrSlug as string);
    
    const endpoint = isGuid 
      ? `/api/products/${idOrSlug}`
      : `/api/products/slug/${idOrSlug}`;

    fetch(endpoint)
      .then(res => res.json())
      .then(data => setProduct(data))
      .catch(console.error);
  }, [idOrSlug]);

  // ... rest of component
}
```

### 2. React Router v6

```typescript
// App.tsx hoặc router config
import { Routes, Route, useParams } from 'react-router-dom';

function ProductDetail() {
  const { slug } = useParams<{ slug: string }>();
  const [product, setProduct] = useState<Product | null>(null);

  useEffect(() => {
    if (!slug) return;
    
    fetch(`${API_BASE_URL}/api/products/slug/${slug}`)
      .then(res => res.json())
      .then(data => setProduct(data))
      .catch(console.error);
  }, [slug]);

  // ... render product
}

// Routes
<Routes>
  <Route path="/products/:slug" element={<ProductDetail />} />
</Routes>
```

### 3. Vue.js / Nuxt.js

```vue
<!-- pages/products/[slug].vue (Nuxt 3) -->
<template>
  <div v-if="product">
    <h1>{{ product.name }}</h1>
    <p>Price: {{ formatPrice(product.price) }} ₫</p>
  </div>
</template>

<script setup>
const route = useRoute();
const { slug } = route.params;

const { data: product } = await useFetch(`/api/products/slug/${slug}`);
</script>
```

### 4. Angular

```typescript
// product-detail.component.ts
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-product-detail',
  template: `
    <div *ngIf="product">
      <h1>{{ product.name }}</h1>
      <p>Price: {{ product.price | number }} ₫</p>
    </div>
  `
})
export class ProductDetailComponent implements OnInit {
  product: Product | null = null;

  constructor(
    private route: ActivatedRoute,
    private http: HttpClient
  ) {}

  ngOnInit() {
    this.route.params.subscribe(params => {
      const slug = params['slug'];
      this.http.get<Product>(`/api/products/slug/${slug}`)
        .subscribe(data => this.product = data);
    });
  }
}

// app-routing.module.ts
const routes: Routes = [
  { path: 'products/:slug', component: ProductDetailComponent }
];
```

### 5. Update Product List Links

**Trước đây (dùng ID):**
```tsx
<Link to={`/products/${product.id}`}>
  {product.name}
</Link>
```

**Bây giờ (dùng slug - SEO-friendly):**
```tsx
<Link to={`/products/${product.slug}`}>
  {product.name}
</Link>
```

## SEO Best Practices

### 1. Canonical URL

Đảm bảo sử dụng slug làm canonical URL:

```tsx
<Head>
  <link rel="canonical" href={`https://yoursite.com/products/${product.slug}`} />
</Head>
```

### 2. Meta Tags

```tsx
<Head>
  <title>{product.name} | Your Store</title>
  <meta name="description" content={product.description} />
  <meta property="og:title" content={product.name} />
  <meta property="og:url" content={`https://yoursite.com/products/${product.slug}`} />
  <meta property="og:image" content={product.primaryImageUrl} />
</Head>
```

### 3. Sitemap

Tạo sitemap với slug URLs:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>https://yoursite.com/products/ao-polo-pickleball-performance</loc>
    <lastmod>2024-01-01</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.8</priority>
  </url>
</urlset>
```

### 4. Redirect từ ID sang Slug (Optional)

Nếu bạn muốn redirect từ URL cũ (ID) sang URL mới (slug):

```typescript
// middleware.ts (Next.js) hoặc middleware trong framework khác
export function middleware(request: Request) {
  const url = new URL(request.url);
  
  // Nếu là product URL với GUID
  if (url.pathname.match(/^\/products\/[0-9a-f-]{36}$/i)) {
    // Fetch product để lấy slug
    // Redirect đến /products/{slug}
  }
}
```

## Helper Functions

### Generate Slug từ Product Name (nếu cần client-side)

```typescript
// utils/slugHelper.ts
export function generateSlug(text: string): string {
  return text
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '') // Remove accents
    .replace(/[^a-z0-9\s-]/g, '') // Remove special chars
    .replace(/\s+/g, '-') // Replace spaces with hyphens
    .replace(/-+/g, '-') // Replace multiple hyphens with single
    .trim();
}

// Usage
const slug = generateSlug('Áo Polo Pickleball Performance');
// Result: 'ao-polo-pickleball-performance'
```

## Migration Checklist

- [ ] Update API calls từ `/api/products/{id}` sang `/api/products/slug/{slug}`
- [ ] Update routing từ `/products/:id` sang `/products/:slug`
- [ ] Update tất cả links trong product list/cards
- [ ] Update sitemap với slug URLs
- [ ] Update canonical URLs
- [ ] Test backward compatibility (nếu cần)
- [ ] Update sharing links (social media)
- [ ] Test 404 handling khi slug không tồn tại

## Error Handling

```typescript
async function fetchProduct(slug: string) {
  try {
    const response = await fetch(`/api/products/slug/${slug}`);
    
    if (!response.ok) {
      if (response.status === 404) {
        // Redirect to 404 page hoặc show error
        throw new Error('Product not found');
      }
      throw new Error('Failed to fetch product');
    }
    
    return await response.json();
  } catch (error) {
    console.error('Error fetching product:', error);
    // Handle error (redirect, show message, etc.)
  }
}
```

## Testing

```typescript
// Test cases cần kiểm tra:
// 1. URL với slug hợp lệ: /products/ao-polo-pickleball-performance
// 2. URL với slug không tồn tại: /products/non-existent-slug
// 3. URL với ID (backward compatible): /products/123e4567-...
// 4. Product list có slug trong response
// 5. Links trong product cards sử dụng slug
```

## Lưu ý quan trọng

1. **Slug luôn có trong API response** - Không cần generate ở client
2. **Slug là unique** - Backend đảm bảo không trùng
3. **Backward compatible** - Vẫn hỗ trợ ID nếu cần
4. **Auto-generate** - Slug tự động tạo khi create/update product
5. **Update slug** - Khi đổi tên sản phẩm, slug sẽ tự động update

## Liên hệ

Nếu có vấn đề hoặc cần hỗ trợ, vui lòng liên hệ backend team.

