using JsonApiDotNetCore.Services;
using SIL.Transcriber;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Configuration;
//using UnitTests;

namespace TranscriberAPI.Tests
{
    public class TestStartup : Startup
    {
        public TestStartup(IHostingEnvironment env) : base(
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
