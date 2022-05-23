using JsonApiDotNetCore.Configuration;
using Microsoft.OpenApi.Models;
using SIL.Transcriber;
using System.Text.Json.Serialization;

WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();
builder.Services.AddMvc().AddJsonOptions(options => {
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
});
builder.Services.AddContextServices();
builder.Services.AddApiServices();
builder.Services.AddAuthenticationServices();
// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v12.6",
        Title = "Transcriber API",
        Contact = new OpenApiContact
        {
            Name = "Sara Hentzel",
            Email = "sara_hentzel@sil.org",
        },
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\"",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

});


WebApplication? app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(builder =>
    {
        builder.WithOrigins("http://localhost:44370")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
}
else
{
    app.UseCors(builder => builder.WithOrigins("http://localhost:44370").AllowAnyHeader());
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseAuthorization();
// Add JsonApiDotNetCore middleware.
app.UseJsonApi();
app.MapControllers();

app.Run();