using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceLowStockTests
{
    [Fact]
    public async Task GetLowStock_FiltersByThresholdAndSortsByStockAscending()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);

        var low = TestSetup.AddProduct(db, stock: 3, sku: "SKU-LOW");
        var mid = TestSetup.AddProduct(db, stock: 8, sku: "SKU-MID");
        TestSetup.AddProduct(db, stock: 20, sku: "SKU-HIGH");

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Equal(low.Id, result[0].Product.Id);
        Assert.Equal(mid.Id, result[1].Product.Id);
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);

        TestSetup.AddProduct(db, stock: 2, isActive: false, sku: "SKU-INACTIVE");
        var active = TestSetup.AddProduct(db, stock: 2, isActive: true, sku: "SKU-ACTIVE");

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal(active.Id, result[0].Product.Id);
    }

    [Fact]
    public async Task GetLowStock_SoldLast30Days_ExcludesCancelledAndOldOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 5, sku: "SKU-SOLD");

        TestSetup.AddOrder(db, customer.Id, OrderStatus.Confirmed, DateTime.UtcNow.AddDays(-10), (product.Id, 4));
        TestSetup.AddOrder(db, customer.Id, OrderStatus.Cancelled, DateTime.UtcNow.AddDays(-5), (product.Id, 100));
        TestSetup.AddOrder(db, customer.Id, OrderStatus.Confirmed, DateTime.UtcNow.AddDays(-40), (product.Id, 100));

        var result = await service.GetLowStockAsync(10);

        var item = Assert.Single(result);
        Assert.Equal(4, item.SoldLast30Days);
    }
}
