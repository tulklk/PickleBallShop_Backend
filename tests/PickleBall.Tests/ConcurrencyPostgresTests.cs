using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PickleBall.Domain.Entities;
using PickleBall.Tests.Infrastructure;

namespace PickleBall.Tests;

/// <summary>
/// Proves that stock and voucher limits hold under real concurrency. The EF InMemory provider is not atomic, so these
/// tests need PostgreSQL and are SKIPPED unless TEST_POSTGRES_CONNECTION is set, e.g.
///   $env:TEST_POSTGRES_CONNECTION = "Host=localhost;Port=5432;Username=postgres;Password=...;Database=postgres"
/// A brand-new throwaway database is created for every test and dropped afterwards, so no existing data is touched.
/// Remote hosts are refused unless TEST_POSTGRES_ALLOW_REMOTE=1 (never point this at production).
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        var conn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            Skip = "Set TEST_POSTGRES_CONNECTION to run PostgreSQL concurrency tests.";
        }
    }
}

public class ConcurrencyPostgresTests : IAsyncLifetime
{
    private string _adminConnection = string.Empty;
    private string _databaseName = string.Empty;
    private ApiFactory? _factory;

    public async Task InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return; // skipped test
        }

        var builder = new NpgsqlConnectionStringBuilder(raw);
        var host = builder.Host ?? string.Empty;
        var local = host is "localhost" or "127.0.0.1" or "::1";
        if (!local && Environment.GetEnvironmentVariable("TEST_POSTGRES_ALLOW_REMOTE") != "1")
        {
            throw new InvalidOperationException(
                "Refusing to run concurrency tests against a non-local PostgreSQL host. Set TEST_POSTGRES_ALLOW_REMOTE=1 to override (never use production).");
        }

        _adminConnection = raw;
        _databaseName = "pb_concurrency_" + Guid.NewGuid().ToString("N")[..12];

        await using (var admin = new NpgsqlConnection(_adminConnection))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        builder.Database = _databaseName;
        _factory = new ApiFactory(false, null, builder.ConnectionString);
        _factory.CreateClient().Dispose(); // boots the host: applies migrations to the empty database
    }

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        if (string.IsNullOrEmpty(_databaseName))
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    private async Task<HttpStatusCode[]> PlaceOrdersInParallelAsync(int count, Func<int, object> body)
    {
        var client = _factory!.CreateClient();
        using var gate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, count).Select(i => Task.Run(async () =>
        {
            gate.Wait();
            var response = await client.PostAsJsonAsync("/api/guest/orders", body(i), TestHelpers.Json);
            return response.StatusCode;
        })).ToArray();

        gate.Set();
        return await Task.WhenAll(tasks);
    }

    [PostgresFact]
    public async Task Parallel_orders_never_oversell_stock()
    {
        const int stock = 5;
        const int attempts = 25;
        var product = await _factory!.CreateProductAsync(stock: stock);

        var results = await PlaceOrdersInParallelAsync(attempts,
            i => TestHelpers.GuestOrderBody(product.Id, 1, TestHelpers.UniqueEmail("race" + i)));

        var succeeded = results.Count(s => s == HttpStatusCode.OK);
        var remaining = await _factory.StockOfAsync(product.Id);
        var ordersCreated = await _factory.WithDb(db => db.OrderItems.CountAsync(i => i.ProductId == product.Id));

        Assert.Equal(stock, succeeded);
        Assert.Equal(0, remaining);
        Assert.Equal(stock, ordersCreated);
        Assert.All(results, s => Assert.True(s == HttpStatusCode.OK || s == HttpStatusCode.BadRequest, $"unexpected {s}"));
    }

    [PostgresFact]
    public async Task Parallel_orders_never_exceed_the_voucher_usage_limit()
    {
        const int limit = 3;
        const int attempts = 20;
        var product = await _factory!.CreateProductAsync(stock: 1000);
        var voucher = new Voucher
        {
            Code = "RACE" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Race voucher",
            DiscountType = VoucherDiscountType.Percent,
            DiscountValue = 10,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            IsPublic = true,
            UsageLimitTotal = limit
        };
        await _factory.WithDb(async db =>
        {
            db.Vouchers.Add(voucher);
            await db.SaveChangesAsync();
        });

        var results = await PlaceOrdersInParallelAsync(attempts,
            i => TestHelpers.GuestOrderBody(product.Id, 1, TestHelpers.UniqueEmail("v" + i), voucherCode: voucher.Code));

        var used = await _factory.WithDb(db => db.Vouchers.AsNoTracking().Where(v => v.Id == voucher.Id).Select(v => v.UsedCount).FirstAsync());
        var ordersWithVoucher = await _factory.WithDb(db => db.Orders.CountAsync(o => o.VoucherId == voucher.Id));

        Assert.True(used <= limit, $"voucher used {used} times, limit {limit}");
        Assert.Equal(used, ordersWithVoucher);
        Assert.Equal(limit, results.Count(s => s == HttpStatusCode.OK));
    }
}
