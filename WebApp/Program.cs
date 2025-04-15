using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;
using WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Add DB context using the same connection string as the ReverseProxy project
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ??
                    "Data Source=../ReverseProxy/reverseproxy.db"));

// Register HttpClient and ReverseProxyService
builder.Services.AddHttpClient<ReverseProxyService>();
builder.Services.AddScoped<ReverseProxyService>();

// Add configuration for ReverseProxySettings if not present
if (builder.Configuration.GetSection("ReverseProxySettings").GetSection("BaseUrl").Value == null)
{
    builder.Configuration["ReverseProxySettings:BaseUrl"] = "http://localhost:5000";
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
