using JsonApiDotNetCore.Configuration;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using System.Text.Json.Serialization;

namespace SIL.Transcriber
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            services.AddCors();
            services.AddMvc();
            services.AddContextServices();
            services.AddApiServices();
            services.AddAuthenticationServices();

            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwagger();
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
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
                app.UseCors(builder =>
                {
                    builder
                        .WithOrigins("http://localhost:44370")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            }
            else
            {
                app.UseCors(builder => builder.WithOrigins(GetAllowedOrigins()).AllowAnyHeader());
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            AWSLoggerConfigSection config = Configuration.GetAWSLoggingConfigSection();
            loggerFactory.AddAWSProvider(config);

            app.UseAuthentication();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseRouting();
            app.UseAuthorization();
            app.UseJsonApi();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }

        private static string GetAllowedOrigins()
        {
            return GetVarOrDefault("SIL_TR_ORIGINSITES", "*");
        }
    }
}
