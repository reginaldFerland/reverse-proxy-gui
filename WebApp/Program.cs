using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;
using WebApp.Configuration;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("Starting ReverseProxyGui... ");
Console.WriteLine($"Version: {typeof(Program).Assembly.GetName().Version}");
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"DbConnection: {builder.Configuration.GetConnectionString("DefaultConnection")}");

// Configure services
builder.ConfigureServices();

var app = builder.Build();

// Configure the application
await app.ConfigureApplicationAsync();

app.Run();
