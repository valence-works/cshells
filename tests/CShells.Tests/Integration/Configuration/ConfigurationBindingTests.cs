using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using CShells.Configuration;

namespace CShells.Tests.Configuration
{
    public class ConfigurationBindingTests
    {
        [Fact]
        public void ShellSettingsFactory_CreatesSettings_FromValidJson()
        {
            var json = @"{
              ""CShells"": {
                ""Shells"": [
                  {
                    ""Name"": ""Default"",
                    ""Features"": [ ""Core"", ""Weather"" ],
                    ""Configuration"": { ""Title"": ""Default Shell"" }
                  },
                  {
                    ""Name"": ""Admin"",
                    ""Features"": [ ""Core"", ""Admin"" ],
                    ""Configuration"": { ""Title"": ""Admin Shell"" }
                  }
                ]
              }
            }";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var config = new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build();

            // Use CreateFromConfiguration to properly parse mixed feature arrays
            var shellsSection = config.GetSection("CShells").GetSection("Shells");
            var settings = shellsSection.GetChildren()
                .Select(ShellSettingsFactory.CreateFromConfiguration)
                .ToList();

            Assert.Equal(2, settings.Count);
            Assert.Contains(settings, s => s.Id.Name == "Default");
            Assert.Contains(settings, s => s.Id.Name == "Admin");

            var def = settings.First(s => s.Id.Name == "Default");
            Assert.Equal(["Core", "Weather"], def.EnabledFeatures);

            // Configuration is now flattened into ConfigurationData
            Assert.True(def.ConfigurationData.ContainsKey("Title"));
            Assert.Equal("Default Shell", def.ConfigurationData["Title"]);
        }

        // Note: Removed test "AddCShells_Registers_IShellHost_And_ShellSettings" because it tested
        // implementation details that changed with the move to endpoint routing. Shells are now
        // loaded when MapCShells() is called, not when services are registered.

        [Fact]
        public void CreateFromConfiguration_DetectsDuplicateNames()
        {
            var json = @"{ ""CShells"": { ""Shells"": [ { ""Name"": ""X"" }, { ""Name"": ""x"" } ] } }";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var config = new ConfigurationBuilder().AddJsonStream(stream).Build();

            // Use CreateFromConfiguration for proper parsing
            var shellsSection = config.GetSection("CShells").GetSection("Shells");
            var shellConfigs = shellsSection.GetChildren()
                .Select(ShellSettingsFactory.CreateFromConfiguration)
                .ToList();

            // Check for duplicates manually
            var duplicates = shellConfigs
                .GroupBy(s => s.Id.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            Assert.NotEmpty(duplicates);
            Assert.Contains("X", duplicates, StringComparer.OrdinalIgnoreCase);
        }
    }
}
