using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.Providers.FluentStorage;
using FluentStorage;

var builder = WebApplication.CreateBuilder(args);

// Register CShells services and configure multi-tenant shell resolution.
// This sample demonstrates a payment processing SaaS platform where different tenants
// have different features and service implementations loaded from JSON files in the Shells folder.
// Shell configurations are loaded from the Shells folder using FluentStorage's disk provider.
// Each JSON file (Default.json, Acme.json, Contoso.json) represents a shell configuration.
// Path mappings are configured in the shell JSON files via the "properties" section.

// Configure FluentStorage to read shell configurations from the Shells folder
var shellsPath = Path.Combine(builder.Environment.ContentRootPath, "Shells");
var blobStorage = StorageFactory.Blobs.DirectoryFiles(shellsPath);

// Register CShells with FluentStorage provider and auto-configure resolvers from shell properties
builder.AddCShells(cshells =>
{
    // Load shell settings from FluentStorage
    cshells.WithFluentStorageProvider(blobStorage);

    // Automatically register Path/Host resolvers from shell properties
    cshells.WithAutoResolvers();
}, assemblies: [typeof(Program).Assembly]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable shell resolution middleware - resolves tenant based on request path
// and activates the appropriate features with their endpoints.
app.UseCShells();

app.Run();

// Make Program class accessible for WebApplicationFactory
public partial class Program { }