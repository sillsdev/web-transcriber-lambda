using JsonApiDotNetCore.Services;
using SIL.Transcriber;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;

namespace TranscriberAPI.Tests
{
    public class TestStartup : Startup
    {
        public TestStartup(IWebHostEnvironment env) : base(
                    new ConfigurationBuilder()
                    .SetBasePath(env.ContentRootPath)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                    .AddEnvironmentVariables().Build(), 
                    env)
        {
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.AddScoped<IScopedServiceProvider, TestScopedServiceProvider>();
            services.BuildServiceProvider();
        }
    }
}
