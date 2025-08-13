using CommentToGame.Data;
using CommentToGame.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;



var builder = WebApplication.CreateBuilder(args);

// Mongo settings & repo
builder.Services.AddSingleton<MongoDbService>();

// 🔐 JWT key'i güvenli al
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
            RoleClaimType  = ClaimTypes.Role
        };
    });
builder.Services.AddHttpClient<IRawgClient, RawgClient>();
builder.Services.AddSingleton<RawgImportService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IgdbAuthService>();
builder.Services.AddSingleton<IIgdbClient, IgdbClient>();
builder.Services.AddSingleton<IgdbImportService>();
builder.Services.AddSingleton<GameEditService>();

// Swagger…
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "CommentToGame API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey, Scheme = "Bearer", BearerFormat = "JWT",
        Description = "JWT gir: Bearer {token}"
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme{ Reference = new OpenApiReference{ Type = ReferenceType.SecurityScheme, Id = "Bearer"} }, new string[]{} }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var mongo = scope.ServiceProvider.GetRequiredService<CommentToGame.Data.MongoDbService>();
    await CommentToGame.Infrastructure.MongoIndexBootstrapper.CreateAsync(mongo);
}

app.MapControllers();
app.Run();
