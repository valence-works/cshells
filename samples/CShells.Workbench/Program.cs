using CShells.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddShells();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapShells();

app.Run();

// Make Program class accessible for WebApplicationFactory
public partial class Program;