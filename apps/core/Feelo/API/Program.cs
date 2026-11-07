using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using API;
using API.Mappings;
using App.Interfaces;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.MapType<TimeOnly>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.String,
        Pattern = @"^\d{2}:\d{2}:\d{2}(\.\d{1,7})?$",
        Examples = new List<JsonNode>
        {
            JsonValue.Create("23:00:00")!
        }
    });

    options.MapType<TimeSpan>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.String,
        Pattern = @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$",
        Examples = new List<JsonNode>
        {
            JsonValue.Create("08:00:00")!
        }
    });
    
    options.SupportNonNullableReferenceTypes();
    options.NonNullableReferenceTypesAsRequired();
    
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token"
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddAuthorization();

var jwtKey = builder.Configuration["jwt:Key"];
if (jwtKey == null)
    throw new ArgumentNullException($"JWT key is empty!");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidAudience = "ProfitableViewAPI",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine("AUTH FAILED: " + ctx.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = _ =>
            {
                Console.WriteLine("TOKEN OK");
                return Task.CompletedTask;
            }
        };
    });

var connection = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<DBContext>(options => options.UseNpgsql(connection));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthTokenService, AuthTokenService>();
builder.Services.AddScoped<PasswordHasher<string>>();
builder.Services.AddScoped<ApiMapper>();
builder.Services.AddScoped<ISurveyService, SurveyService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ICircadianService, CircadianService>();
builder.Services.AddScoped<AuthTokenService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DBContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapEndpoints();
app.Run();
