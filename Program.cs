using CommentToGame.Data;
using CommentToGame.Services;
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
    });

builder.Services.AddHttpClient<IRawgClient, RawgClient>();
builder.Services.AddSingleton<RawgImportService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IgdbAuthService>();
builder.Services.AddSingleton<IIgdbClient, IgdbClient>();
builder.Services.AddSingleton<IgdbImportService>();
builder.Services.AddSingleton<GameEditService>();

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
            .AllowCredentials(); // gerekiyorsa
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
app.UseAuthorization();

// Mongo index bootstrap (async scope)
using (var scope = app.Services.CreateScope())
{
    var mongo = scope.ServiceProvider.GetRequiredService<MongoDbService>();
    await CommentToGame.Infrastructure.MongoIndexBootstrapper.CreateAsync(mongo);
}

app.MapControllers();
app.Run();