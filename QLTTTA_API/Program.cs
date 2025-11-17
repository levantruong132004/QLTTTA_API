using QLTTTA_API.Services;
using System.Text.Json; // add

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Force the API to run on HTTP port 7158 to avoid HTTPS certificate prompts on localhost.
    builder.WebHost.UseUrls("http://localhost:7158");

    // Add services to the container.
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        });

    // Services registration
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUserCredentialCache, InMemoryUserCredentialCache>();
    builder.Services.AddScoped<IOtpStore, InMemoryOtpStore>();
    builder.Services.AddScoped<IEmailService, MailKitEmailService>();
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
    builder.Services.AddScoped<IDigitalSignatureService, DigitalSignatureService>();
    builder.Services.AddScoped<IPdfSignatureService, PdfSignatureService>();

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowWebApp", policy =>
        {
            policy.WithOrigins(
                      "http://localhost:7169",
                      "https://localhost:7169",
                      "http://localhost:5165",
                      "https://localhost:5165") // Port của web app
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

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
