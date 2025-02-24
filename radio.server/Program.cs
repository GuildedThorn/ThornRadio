using System;
using System.Text.Json;
using System.Threading.Tasks;
using Radio.Server.Services;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;
var keyString = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");

if (string.IsNullOrEmpty(keyString)) throw new InvalidOperationException("JWT signing key is not configured.");

var keyBytes = Convert.FromBase64String(keyString);
var key = new SymmetricSecurityKey(keyBytes);

services.AddHttpContextAccessor();  // Add IHttpContextAccessor
services.AddSingleton(key);

// Configure JWT Authentication
services.AddAuthorizationBuilder()
    .AddPolicy("PrivilegedOnly", policy => policy.RequireRole("owner", "user"));
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = key
        };
        options.Events = new JwtBearerEvents {
            OnMessageReceived = context => {
                // Check both cookie and query string for token
                context.Token = context.Request.Cookies["token"] 
                              ?? context.Request.Query["access_token"];
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddCors(options => {
    options.AddPolicy(name: "AllowSpecificOrigin",
        policy => {
            policy.WithOrigins("http://localhost:5173", "https://localhost:5173") // Allow frontend
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

// Register MongoDbService and ChatService
services.AddSingleton<MongoDbService>();
services.AddScoped<ChatService>();

// Add SignalR and configure for LongPolling
services.AddSignalR(hubOptions => {
    hubOptions.EnableDetailedErrors = true;
    hubOptions.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
}).AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});;

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

var app = builder.Build();

// Middleware configuration
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
// app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("AllowSpecificOrigin");
app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hub
app.MapHub<ChatHub>("/chathub", options => {
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
}).RequireCors("AllowSpecificOrigin");

// Default controller route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
