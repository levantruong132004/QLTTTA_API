using QLTTTA_API.Services;
using System.Text.Json; // add

try
{
    var builder = WebApplication.CreateBuilder(args);

    // builder.WebHost.UseUrls("http://localhost:7158"); // Removed to allow Cloudflare/Env config

    // Add services to the container.
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        });

    // Configure ForwardedHeaders for Cloudflare/Proxy
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | 
                                   Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear(); // Trust all networks (Cloudflare IPs vary)
        options.KnownProxies.Clear();
    });

    // Đăng ký services
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<IUserCredentialCache, InMemoryUserCredentialCache>();
    builder.Services.AddSingleton<IOtpStore, InMemoryOtpStore>();
    builder.Services.AddSingleton<IEmailService, MailKitEmailService>();
    builder.Services.AddScoped<IOracleConnectionProvider, OracleUserConnectionProvider>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IStudentService, StudentService>();
    builder.Services.AddScoped<ICourseService, CourseService>();
    builder.Services.AddScoped<IProfileService, ProfileService>();
    builder.Services.AddScoped<IClassService, ClassService>();
    builder.Services.AddScoped<IScheduleService, ScheduleService>();
    builder.Services.AddScoped<IRegistrationService, RegistrationService>();
    builder.Services.AddScoped<IInvoiceService, InvoiceService>();
    builder.Services.AddScoped<IPaymentService, PaymentService>();
    builder.Services.AddScoped<IDigitalSignatureService, DigitalSignatureService>(); // Thêm service chữ ký số
    builder.Services.AddScoped<IPdfSignatureService, PdfSignatureService>();
    builder.Services.AddSingleton<IQrLoginService, InMemoryQrLoginService>();
    builder.Services.AddScoped<IStaffService, StaffService>();
    builder.Services.AddScoped<IAdminStaffService, AdminStaffService>();

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowWebApp", policy =>
        {
            policy.AllowAnyOrigin() // Allow Cloudflare/Any origin
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Use ForwardedHeaders
    app.UseForwardedHeaders();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Use CORS
    app.UseCors("AllowWebApp");

    app.UseAuthorization();

    app.MapControllers();

    Console.WriteLine("Starting application...");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Application startup error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
