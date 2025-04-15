using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;
using WebApp.Services;
using WebApp.Configuration;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.ConfigureServices();

var app = builder.Build();

// Configure the application
await app.ConfigureApplicationAsync();

// Ensure SQLitePCL is initialized properly
SQLitePCL.Batteries_V2.Init();

app.Run();
