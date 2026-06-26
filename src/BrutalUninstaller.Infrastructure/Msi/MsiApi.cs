using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using BrutalUninstaller.Core.Interfaces;

namespace BrutalUninstaller.Infrastructure.Msi;

public sealed class MsiApi : IMsiApi
{
    private readonly ILogger<MsiApi> _logger;

    public MsiApi(ILogger<MsiApi> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<(string productCode, string productName)>> EnumProductsAsync()
    {
        var products = new List<(string productCode, string productName)>();

        await Task.Run(() =>
        {
            var sbProductCode = new StringBuilder(39); // 38 chars + null terminator
            var productIndex = 0;

            while (true)
            {
                sbProductCode.Clear();
                var result = NativeMethods.MsiEnumProducts(productIndex, sbProductCode);

                if (result != 0)
                    break;

                var productCode = sbProductCode.ToString();

                // Get the product name for this product code
                var productName = GetProductInfoSync(productCode, "InstalledProductName");
                var displayName = productName ?? GetProductInfoSync(productCode, "ProductName") ?? "Unknown";

                products.Add((productCode, displayName));
                _logger.LogTrace("Enumerated MSI product: {ProductCode} = {ProductName}", productCode, displayName);

                productIndex++;
            }
        });

        _logger.LogInformation("Enumerated {Count} MSI products", products.Count);
        return products;
    }

    /// <inheritdoc />
    public async Task<int> ConfigureProductAsync(string productCode, int installLevel = 0, int installState = -1)
    {
        ArgumentException.ThrowIfNullOrEmpty(productCode);

        return await Task.Run(() =>
        {
            _logger.LogInformation("Configuring MSI product: {ProductCode} (InstallLevel: {Level}, InstallState: {State})",
                productCode, installLevel, installState);

            var result = NativeMethods.MsiConfigureProduct(productCode, installLevel, installState);

            _logger.LogInformation("MsiConfigureProduct returned {Result} for {ProductCode}", result, productCode);
            return result;
        });
    }

    /// <inheritdoc />
    public async Task<string?> GetProductInfoAsync(string productCode, string property)
    {
        ArgumentException.ThrowIfNullOrEmpty(productCode);
        ArgumentException.ThrowIfNullOrEmpty(property);

        return await Task.Run(() => GetProductInfoSync(productCode, property));
    }

    /// <inheritdoc />
    public bool IsMsiProduct(string productCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(productCode);

        var hProduct = IntPtr.Zero;
        try
        {
            var result = NativeMethods.MsiOpenProduct(productCode, out hProduct);
            return result == 0;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Error checking MSI product: {ProductCode}", productCode);
            return false;
        }
        finally
        {
            if (hProduct != IntPtr.Zero)
            {
                NativeMethods.MsiCloseHandle(hProduct);
            }
        }
    }

    private string? GetProductInfoSync(string productCode, string property)
    {
        var sbBuf = new StringBuilder(256);
        var bufSize = sbBuf.Capacity;

        var result = NativeMethods.MsiGetProductInfo(productCode, property, sbBuf, ref bufSize);

        if (result == 0)
        {
            return sbBuf.ToString();
        }

        if (result == NativeMethods.ERROR_MORE_DATA)
        {
            // Retry with larger buffer
            sbBuf = new StringBuilder(bufSize);
            result = NativeMethods.MsiGetProductInfo(productCode, property, sbBuf, ref bufSize);
            if (result == 0)
            {
                return sbBuf.ToString();
            }
        }

        return null;
    }

    private static class NativeMethods
    {
        public const int ERROR_MORE_DATA = 234;

        [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int MsiEnumProducts(
            int productIndex,
            StringBuilder productCode);

        [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int MsiGetProductInfo(
            string productCode,
            string property,
            StringBuilder valueBuf,
            ref int valueBufSize);

        [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int MsiConfigureProduct(
            string productCode,
            int installLevel,
            int installState);

        [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int MsiOpenProduct(
            string productCode,
            out IntPtr hProduct);

        [DllImport("msi.dll", ExactSpelling = true)]
        public static extern int MsiCloseHandle(IntPtr hAny);
    }
}
