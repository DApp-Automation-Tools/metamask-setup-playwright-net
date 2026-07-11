global using Xunit;
global using MetamaskSetup.Playwright.Services;
global using MetamaskSetup.Playwright.Models;
global using MetamaskSetup.Playwright.Tests.Configuration;
global using MetamaskSetup.Playwright.Tests.Fixtures;
global using MetamaskSetup.Playwright.Tests.Helpers;
global using Microsoft.Playwright;
// Alias to disambiguate from the MetamaskSetup.Playwright library namespace
global using PlaywrightFactory = Microsoft.Playwright.Playwright;

// All tests in this assembly run sequentially — browser sessions holding extension
// profiles cannot safely share temp directories across parallel workers.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
