using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data;

public static class SeedData
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (!context.ProductCategories.Any())
        {
            var categories = new List<ProductCategory>
            {
                new ProductCategory
                {
                    Id = Guid.NewGuid(),
                    Name = "Bóng Pickleball",
                    Slug = "bong-pickleball",
                    Description = "Các loại bóng chơi pickleball chính hãng",
                },
                new ProductCategory
                {
                    Id = Guid.NewGuid(),
                    Name = "Vợt Pickleball",
                    Slug = "vot-pickleball",
                    Description = "Vợt pickleball cao cấp cho mọi trình độ",
                },
                new ProductCategory
                {
                    Id = Guid.NewGuid(),
                    Name = "Lưới Pickleball",
                    Slug = "luoi-pickleball",
                    Description = "Lưới pickleball chuyên nghiệp",
                },
                new ProductCategory
                {
                    Id = Guid.NewGuid(),
                    Name = "Trang phục",
                    Slug = "trang-phuc",
                    Description = "Quần áo chơi pickleball",
                },
                new ProductCategory
                {
                    Id = Guid.NewGuid(),
                    Name = "Giày",
                    Slug = "giay",
                    Description = "Giày chơi pickleball chuyên dụng",
                },
                new ProductCategory
                {
                    Id = Guid.NewGuid(),
                    Name = "Phụ kiện",
                    Slug = "phu-kien",
                    Description = "Các phụ kiện hỗ trợ chơi pickleball",
                }
            };

            context.ProductCategories.AddRange(categories);
            await context.SaveChangesAsync();

            // Add products
            if (!context.Products.Any())
            {
                var products = new List<Product>
                {
                    new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = "Bóng Pickleball Onix Fuse G2 Outdoor",
                        Slug = "bong-pickleball-onix-fuse-g2-outdoor",
                        Description = "Bóng pickleball chuyên nghiệp cho sân ngoài trời, bền và có độ nảy tốt",
                        CategoryId = categories[0].Id,
                        Price = 250000,
                        OriginalPrice = 300000,
                        StockQuantity = 100,
                        SKU = "PB-ONIX-FUSE-G2-OUT",
                        Brand = "Onix",
                        IsActive = true
                    },
                    new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = "Vợt Pickleball Selkirk Amped Invikta",
                        Slug = "vot-pickleball-selkirk-amped-invikta",
                        Description = "Vợt pickleball cao cấp với cán dài, phù hợp cho người chơi có trình độ",
                        CategoryId = categories[1].Id,
                        Price = 3500000,
                        OriginalPrice = 4000000,
                        StockQuantity = 50,
                        SKU = "PB-SELKIRK-AMPED-INV",
                        Brand = "Selkirk",
                        IsActive = true
                    },
                    new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = "Vợt Pickleball Paddletek Bantam EX-L",
                        Slug = "vot-pickleball-paddletek-bantam-ex-l",
                        Description = "Vợt pickleball nhẹ, dễ chơi cho người mới bắt đầu",
                        CategoryId = categories[1].Id,
                        Price = 2800000,
                        StockQuantity = 75,
                        SKU = "PB-PADDLETEK-BANTAM-EXL",
                        Brand = "Paddletek",
                        IsActive = true
                    },
                    new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = "Lưới Pickleball Regulation Net",
                        Slug = "luoi-pickleball-regulation-net",
                        Description = "Lưới pickleball tiêu chuẩn quốc tế, cao 36 inch ở giữa",
                        CategoryId = categories[2].Id,
                        Price = 1500000,
                        StockQuantity = 30,
                        SKU = "PB-NET-REG-36",
                        Brand = "Pro-Lite",
                        IsActive = true
                    },
                    new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = "Áo Polo Pickleball Performance",
                        Slug = "ao-polo-pickleball-performance",
                        Description = "Áo polo chơi pickleball chất liệu thấm hút mồ hôi, thoáng mát",
                        CategoryId = categories[3].Id,
                        Price = 450000,
                        StockQuantity = 200,
                        SKU = "PB-POLO-PERF-BLUE",
                        Brand = "HEAD",
                        IsActive = true
                    },
                    new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = "Giày Pickleball Court Shoes",
                        Slug = "giay-pickleball-court-shoes",
                        Description = "Giày chơi pickleball chuyên dụng, đế không để lại vết trên sân",
                        CategoryId = categories[4].Id,
                        Price = 1800000,
                        OriginalPrice = 2200000,
                        StockQuantity = 60,
                        SKU = "PB-SHOES-COURT-BLACK",
                        Brand = "ASICS",
                        IsActive = true
                    },
                    new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = "Túi đựng vợt Pickleball Deluxe",
                        Slug = "tui-dung-vot-pickleball-deluxe",
                        Description = "Túi đựng vợt pickleball cao cấp, có ngăn riêng cho phụ kiện",
                        CategoryId = categories[5].Id,
                        Price = 650000,
                        StockQuantity = 80,
                        SKU = "PB-BAG-DELUXE-BLACK",
                        Brand = "Onix",
                        IsActive = true
                    },
                    new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = "Bộ bóng tập Pickleball 100 quả",
                        Slug = "bo-bong-tap-pickleball-100-qua",
                        Description = "Bộ 100 quả bóng pickleball để tập luyện, giá ưu đãi",
                        CategoryId = categories[0].Id,
                        Price = 18000000,
                        OriginalPrice = 25000000,
                        StockQuantity = 25,
                        SKU = "PB-BALL-PRACTICE-100",
                        Brand = "Franklin",
                        IsActive = true
                    }
                };

                context.Products.AddRange(products);
                await context.SaveChangesAsync();

                // Add product images
                var productImages = new List<ProductImage>();
                foreach (var product in products)
                {
                    productImages.Add(new ProductImage
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        ImageUrl = $"https://via.placeholder.com/500x500?text={Uri.EscapeDataString(product.Name)}",
                        IsPrimary = true
                    });
                }

                context.ProductImages.AddRange(productImages);
                await context.SaveChangesAsync();
            }
        }

        // Create admin user if not exists
        if (!context.Users.Any(u => u.Email == "admin@pickleballshop.com"))
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@pickleballshop.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                FullName = "Administrator",
                PhoneNumber = "0123456789",
                Role = Role.Admin,
                IsActive = true
            };

            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
        }
    }
}
