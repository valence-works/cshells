using System.Text;
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
                    ""Properties"": { ""Title"": ""Default Shell"" }
                  },
                  {
                    ""Name"": ""Admin"",
                    ""Features"": [ ""Core"", ""Admin"" ],
                    ""Properties"": { ""Title"": ""Admin Shell"" }
                  }
                ]
              }
            }";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var config = new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build();

            var options = new CShellsOptions();
            config.GetSection("CShells").Bind(options);

            var settings = ShellSettingsFactory.CreateFromOptions(options).ToList();

            Assert.Equal(2, settings.Count);
            Assert.Contains(settings, s => s.Id.Name == "Default");
            Assert.Contains(settings, s => s.Id.Name == "Admin");

            var def = settings.First(s => s.Id.Name == "Default");
            Assert.Equal(["Core", "Weather"], def.EnabledFeatures);
            Assert.Equal("Default Shell", def.Properties["Title"]);
        }

        // Note: Removed test "AddCShells_Registers_IShellHost_And_ShellSettings" because it tested
        // implementation details that changed with the move to endpoint routing. Shells are now
        // loaded when MapCShells() is called, not when services are registered.

        [Fact]
        public void CreateFromOptions_Throws_On_DuplicateNames()
        {
            var json = @"{ ""CShells"": { ""Shells"": [ { ""Name"": ""X"" }, { ""Name"": ""x"" } ] } }";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
            var options = new CShellsOptions();
            config.GetSection("CShells").Bind(options);

            var ex = Assert.Throws<ArgumentException>(() => ShellSettingsFactory.CreateFromOptions(options).ToList());
            Assert.Contains("Duplicate shell name", ex.Message);
        }
    }
}
