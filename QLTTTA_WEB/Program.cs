var builder = WebApplication.CreateBuilder(args);

// Run the MVC app on HTTP port 5165 to avoid localhost certificate warnings.
builder.WebHost.UseUrls("http://localhost:5165");

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
// Redirect to Cloudflare only when not Development and PublicBaseUrl is set
if (!app.Environment.IsDevelopment())
{
    app.Use(async (ctx, next) =>
    {
        var publicBase = app.Configuration["PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(publicBase))
        {
            var accept = ctx.Request.Headers["Accept"].ToString();
            bool wantsHtml = accept.Contains("text/html", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(accept);
            var host = ctx.Request.Host.Host;
            bool isLocalHost = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(host, "127.0.0.1")
                               || string.Equals(host, "::1");
            if (wantsHtml && isLocalHost && Uri.TryCreate(publicBase, UriKind.Absolute, out var pu))
            {
                if (!string.Equals(host, pu.Host, StringComparison.OrdinalIgnoreCase))
                {
                    var target = $"{pu.Scheme}://{pu.Authority}{ctx.Request.PathBase}{ctx.Request.Path}{ctx.Request.QueryString}";
                    ctx.Response.Redirect(target, permanent: false);
                    return;
                }
            }
        }
        await next();
    });
}
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthorization();

// Explicit route for StaffStudents to avoid any discovery quirks
app.MapControllerRoute(
    name: "staffstudents",
    pattern: "StaffStudents/{action=Index}/{id?}",
    defaults: new { controller = "StaffStudents" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapControllerRoute(
    name: "home",
    pattern: "home/{action=Index}/{id?}",
    defaults: new { controller = "Home" });

app.Run();
