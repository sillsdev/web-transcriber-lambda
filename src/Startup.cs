using JsonApiDotNetCore.Configuration;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Logging;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static System.Net.Mime.MediaTypeNames;

namespace SIL.Transcriber
{
    public class Startup
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public virtual void ConfigureServices(IServiceCollection services)
        {
            IdentityModelEventSource.ShowPII = true;
            _ = services.Configure<CookiePolicyOptions>(options => {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            _ = services.AddCors();
            _ = services.AddMvc();
            _ = services.AddContextServices();
            _ = services.AddApiServices();
            _ = services.AddAuthenticationServices();

            _ = services.AddControllers();
            _ = services.AddEndpointsApiExplorer();
            _ = services.AddSwagger();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            ILoggerFactory loggerFactory
        )
        {
            if (env.IsDevelopment())
            {
                //always do what it will do in production
                // _ = app.UseDeveloperExceptionPage();
                _ = app.UseSwagger();
                _ = app.UseSwaggerUI();
            }

            _ = app.UseExceptionHandler(exceptionHandlerApp => {
                exceptionHandlerApp.Run(async context => {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = Text.Plain;

                    IExceptionHandlerPathFeature? exceptionHandlerPathFeature =
                                context.Features.Get<IExceptionHandlerPathFeature>();
                    //circumvent their security features and write the damn information
                    await context.Response.WriteAsync(exceptionHandlerPathFeature?.Error.InnerException?.Message ?? exceptionHandlerPathFeature?.Error.Message ?? "");
                });
            });

            AWSLoggerConfigSection config = Configuration.GetAWSLoggingConfigSection();
            _ = loggerFactory.AddAWSProvider(config);

            _ = app.UseAuthentication();
            _ = app.UseHttpsRedirection();
            _ = app.UseStaticFiles();
            _ = app.UseCookiePolicy();

            _ = app.UseRouting();
            _ = app.UseCors(
                builder =>
                    builder
                        .WithOrigins(GetAllowedOrigins())
                        .SetIsOriginAllowed((host) => host == GetAllowedOrigins() || host == "*")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
            );
            _ = app.UseAuthorization();
            app.UseJsonApi();
            _ = app.UseEndpoints(endpoints => endpoints.MapControllers());
        }

        private static string GetAllowedOrigins()
        {
            return GetVarOrDefault("SIL_TR_ORIGINSITES", "*");
        }
    }
}
