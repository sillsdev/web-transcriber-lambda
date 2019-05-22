
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Extensions;
using Newtonsoft.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using Amazon.S3;
using Amazon.Runtime;

namespace SIL.Transcriber
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostingEnvironment env)
        {
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        private bool IsDevelopment => Environment.IsDevelopment() || Environment.IsEnvironment("Testing");

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
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1).AddJsonOptions(x => x.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);
            services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(GetConnectionString()));
            services.AddApiServices();
            services.AddAuthenticationServices(Configuration);
            services.AddContextServices();
            services.AddSingleton<IS3Service, S3Service>();
            services.AddAWSService<IAmazonS3>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            app.UseAuthentication();

            app.UseCors(builder => builder.WithOrigins(GetAllowedOrigins()).AllowAnyHeader());
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseJsonApi();
        }

        private string GetAllowedOrigins()
        {
            return GetVarOrDefault("OriginSites","*");
        }

        private string GetConnectionString()
        {
            return GetVarOrDefault("ConnectionString", Configuration["ConnectionString"]);
        }
    }
}
