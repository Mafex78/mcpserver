using McpServer.ProductBox.Dto;

namespace McpServer.ProductBox.Repository;

public interface IProductRepository
{
    IEnumerable<Product> Search(string keyword, int maxResults = 10);
    Product? GetBySku(string sku);
}