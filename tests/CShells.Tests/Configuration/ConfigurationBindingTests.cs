using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
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

            var config = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
                .Build();

            var options = new CShellsOptions();
            config.GetSection("CShells").Bind(options);

            var settings = ShellSettingsFactory.CreateFromOptions(options).ToList();

            Assert.Equal(2, settings.Count);
            Assert.Contains(settings, s => s.Id.Name == "Default");
            Assert.Contains(settings, s => s.Id.Name == "Admin");

            var def = settings.First(s => s.Id.Name == "Default");
            Assert.Equal(new[] { "Core", "Weather" }, def.EnabledFeatures);
            Assert.Equal("Default Shell", def.Properties["Title"]);
        }

        [Fact]
        public void AddCShells_Registers_IShellHost_And_ShellSettings()
        {
            var json = @"{ ""CShells"": { ""Shells"": [ { ""Name"": ""Default"", ""Features"": [] } ] } }";
            var config = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();

            var services = new ServiceCollection();
            services.AddCShells(config);

            var sp = services.BuildServiceProvider();

            var host = sp.GetService<IShellHost>();
            Assert.NotNull(host);
            Assert.Contains(host.AllShells, s => s.Id.Name == "Default");
        }

        [Fact]
        public void CreateFromOptions_Throws_On_DuplicateNames()
        {
            var json = @"{ ""CShells"": { ""Shells"": [ { ""Name"": ""X"" }, { ""Name"": ""x"" } ] } }";
            var config = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();
            var options = new CShellsOptions();
            config.GetSection("CShells").Bind(options);

            var ex = Assert.Throws<ArgumentException>(() => ShellSettingsFactory.CreateFromOptions(options).ToList());
            Assert.Contains("Duplicate shell name", ex.Message);
        }
    }
}
