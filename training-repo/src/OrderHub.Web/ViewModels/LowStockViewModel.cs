namespace OrderHub.Web.ViewModels;

public class LowStockViewModel
{
    public int Threshold { get; set; } = 10;
    public IReadOnlyList<LowStockProductRowViewModel> Products { get; set; } = Array.Empty<LowStockProductRowViewModel>();
}

public class LowStockProductRowViewModel
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int SoldLast30Days { get; set; }
}
