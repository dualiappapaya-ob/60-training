using OrderHub.Core.Domain;

namespace OrderHub.Core.Services;

public record LowStockItem(Product Product, int SoldLast30Days);
