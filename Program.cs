using CommentToGame.Data;
using CommentToGame.Services;
using CommentToGame.Models;                    // SystemLogCategory
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ----- SERVICES -----
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<PreviewImportService>();

// JWT
var jwtKey = builder.Configuration.GetValue<string>("Jwt:Key")
             ?? throw new InvalidOperationException("Jwt:Key is missing in configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            RoleClaimType = ClaimTypes.Role
        };

        // 🔔 JWT event'lerinden gerçek log üret
        o.Events = new JwtBearerEvents
    {
    OnAuthenticationFailed = async ctx => { /* ... */ },

    OnTokenValidated = async ctx =>
    {
        var path = ctx.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/auth/login") &&
            !path.StartsWith("/api/auth/refresh"))
            return; // diğer isteklerde loglama

        using var scope = ctx.HttpContext.RequestServices.CreateScope();
        var slog = scope.ServiceProvider.GetRequiredService<ISystemLogger>();
        var userName = ctx.Principal?.Identity?.Name ?? "unknown";
        await slog.InfoAsync(SystemLogCategory.Authentication, "JWT token validated", userName);
    }
    };
    });

builder.Services.AddHttpClient<IRawgClient, RawgClient>();
builder.Services.AddSingleton<RawgImportService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IgdbAuthService>();
builder.Services.AddSingleton<IIgdbClient, IgdbClient>();
builder.Services.AddSingleton<IgdbImportService>();
builder.Services.AddSingleton<GameEditService>();

// 🔧 Log servisi
builder.Services.AddScoped<SystemLogService>();
builder.Services.AddScoped<ISystemLogger, SystemLogger>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "CommentToGame API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Description = "JWT gir: Bearer {token}"
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
    o.CustomSchemaIds(t => (t.FullName ?? t.Name).Replace("+", "."));
});

// CORS ------------- (BUILD'DEN ÖNCE!)
const string CorsPolicy = "DevCors";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "https://localhost:3000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ----- BUILD -----
var app = builder.Build();

// ----- MIDDLEWARE -----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Geliştirmede http kullanacaksan bu satırı yoruma al:
// app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

app.UseAuthentication();

// 🌐 Global exception logging middleware (500'leri yakala)
app.UseMiddleware<CommentToGame.Middleware.ExceptionLoggingMiddleware>();

app.UseAuthorization();

// ❌ Seed DEMO çağrısı YOK; gerçek loglar gelecek.

// Mongo index bootstrap (async scope)
using (var scope = app.Services.CreateScope())
{
    var mongo = scope.ServiceProvider.GetRequiredService<MongoDbService>();
    await CommentToGame.Infrastructure.MongoIndexBootstrapper.CreateAsync(mongo);
}

app.MapControllers();
app.Run();
