using System.Text.Json;
using Allure.Net.Commons;
using Xunit.Abstractions;

[assembly: Xunit.TestFramework("Common.AllureStartup", "Common")]

namespace Common;

public class AllureStartup : XunitTestFramework
{
    public AllureStartup(IMessageSink messageSink) : base(messageSink)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "allure-config.json");
        if (File.Exists(configPath))
        {
            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AllureConfiguration>(configJson);
            AllureLifecycle.Instance.UpdateConfig(config);
        }
    }
}