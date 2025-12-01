using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Providers.FluentStorage;
using FluentStorage;

var builder = WebApplication.CreateBuilder(args);

// Register shells from configuration (appsettings.json):
builder.AddShells();

// Or configure FluentStorage to read shell configurations from the Shells folder
// var shellsPath = Path.Combine(builder.Environment.ContentRootPath, "Shells");
// var blobStorage = StorageFactory.Blobs.DirectoryFiles(shellsPath);
//
// // Register CShells with FluentStorage provider and auto-configure resolvers from shell properties
// builder.AddCShells(cshells =>
// {
//     // Load shell settings from FluentStorage
//     cshells.WithFluentStorageProvider(blobStorage);
//
// }, assemblies: [typeof(Program).Assembly]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable endpoint routing
app.UseRouting();

// Configure CShells middleware and endpoints - resolves tenant based on request path
// and activates the appropriate features with their endpoints.
app.MapShells();

app.Run();

// Make Program class accessible for WebApplicationFactory
public partial class Program;