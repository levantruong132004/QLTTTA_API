using QLTTTA_API.Services;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Force the API to run on HTTP port 7158 to avoid HTTPS certificate prompts on localhost.
    builder.WebHost.UseUrls("http://localhost:7158");

    // Add services to the container.
    builder.Services.AddControllers();

    // Đăng ký services
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<IUserCredentialCache, InMemoryUserCredentialCache>();
    builder.Services.AddScoped<IOracleConnectionProvider, OracleUserConnectionProvider>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IStudentService, StudentService>();
    builder.Services.AddScoped<ICourseService, CourseService>();
    builder.Services.AddScoped<IProfileService, ProfileService>();

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
