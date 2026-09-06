using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OAuthDemo.Api.Data;
using OAuthDemo.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure SQL Server connection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? "Server=(localdb)\\MSSQLLocalDB;Database=OAuthDemo;Trusted_Connection=True;";
    options.UseSqlServer(connectionString);
});

// Add controllers
builder.Services.AddControllers();

// Add session state (for OAuth state parameter CSRF protection)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add authentication with JWT Bearer token
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "JwtBearer";
    options.DefaultChallengeScheme = "JwtBearer";
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(
                builder.Configuration.GetSection("Jwt:Key").Value ?? string.Empty)),

        ValidIssuer = builder.Configuration.GetSection("Jwt:Issuer").Value ?? string.Empty,
        ValidAudience = builder.Configuration.GetSection("Jwt:Audience").Value ?? string.Empty,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Add authorization services
builder.Services.AddAuthorization();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {jwt}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Enable session (for OAuth state parameter)
app.UseSession();

// Map controllers
app.MapControllers();

// Run the application
app.Run();