namespace BrutalUninstaller.Core.Interfaces;

public interface IMsiApi
{
    Task<List<(string productCode, string productName)>> EnumProductsAsync();
    Task<int> ConfigureProductAsync(string productCode, int installLevel = 0, int installState = -1);
    Task<string?> GetProductInfoAsync(string productCode, string property);
    bool IsMsiProduct(string productCode);
}
