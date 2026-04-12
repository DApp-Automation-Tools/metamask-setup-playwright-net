namespace MetamaskSetup.Playwright.Models;

public class NetworkConfig
{
    public required string Name { get; set; }
    public required string RpcUrl { get; set; }
    public required string ChainId { get; set; }
    public required string Symbol { get; set; }
    public string? BlockExplorerUrl { get; set; }
}