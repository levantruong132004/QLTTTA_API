var builder = WebApplication.CreateBuilder(args);

// Run the MVC app on HTTP port 5165 and bind to all interfaces so mobile devices in the same LAN can access it via your PC IP.
builder.WebHost.UseUrls("http://0.0.0.0:5165");

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<QLTTTA_WEB.Infrastructure.SessionHeaderHandler>();
builder.Services.AddHttpClient("ApiClient", client =>
{
    // Địa chỉ của API backend
    client.BaseAddress = new Uri("http://localhost:7158/"); // port API
})
.AddHttpMessageHandler<QLTTTA_WEB.Infrastructure.SessionHeaderHandler>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapControllerRoute(
    name: "home",
    pattern: "home/{action=Index}/{id?}",
    defaults: new { controller = "Home" });

app.Run();
