using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber
{
    public static class BackendServiceExtensions
    {
        public static IServiceCollection AddApiServices(this IServiceCollection services)
        {
            // add jsonapi dotnet core
            // - includes IHttpContextAccessor as a singleton
            services.AddJsonApi<AppDbContext>(options => {
                options.Namespace = "api";
                options.IncludeTotalRecordCount = false;
                
                //options.EnableOperations = true;
            });

            services.AddHttpContextAccessor();

            // Add service / repository overrides
            services.AddScoped<IEntityRepository<User>, UserRepository>();
            //services.AddScoped<IEntityRepository<UserTask>, UserTaskRepository>();
            services.AddScoped<IEntityRepository<Group>, GroupRepository>();
            services.AddScoped<IEntityRepository<Project>, ProjectRepository>();
            services.AddScoped<IEntityRepository<Organization>, OrganizationRepository>();
            //services.AddScoped<IEntityRepository<OrganizationInviteRequest>, OrganizationInviteRequestRepository>();
            //services.AddScoped<IEntityRepository<Notification>, NotificationRepository>();
            services.AddScoped<IUpdateService<Project, int>, ProjectService>();

            // services
            services.AddScoped<IResourceService<User>, UserService>();
            services.AddScoped<IResourceService<UserTask>, UserTaskService>();
            services.AddScoped<IResourceService<Organization>, OrganizationService>();
            services.AddScoped<IResourceService<Group>, GroupService>();
            services.AddScoped<IResourceService<Project>, ProjectService>();
            services.AddScoped<IResourceService<GroupMembership>, GroupMembershipService>();
            services.AddScoped<IResourceService<OrganizationMembership>, OrganizationMembershipService>();
            //services.AddScoped<IResourceService<OrganizationMembershipInvite>, OrganizationMembershipInviteService>();

            //services.AddScoped<IQueryParser, OrbitJSQueryParser>();

            // EventDispatchers
            services.AddScoped<UserRepository>();
            services.AddScoped<GroupRepository>();
            services.AddScoped<ProjectRepository>();
            services.AddScoped<OrganizationRepository>();
            services.AddScoped<CurrentUserRepository>();

            services.AddScoped<UserService>();
            services.AddScoped<OrganizationService>();
            services.AddScoped<GroupService>();
            services.AddScoped<Auth0ManagementApiTokenService>();
            //services.AddScoped<SendNotificationService>();
            //services.AddScoped<SendEmailService>();
            services.AddScoped<OrganizationMembershipService>();
            //services.AddScoped<OrganizationMembershipInviteService>();

            return services;
        }

        public static IServiceCollection AddContextServices(this IServiceCollection services)
        {
            services.AddScoped<IOrganizationContext, HttpOrganizationContext>();
            services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

            return services;
        }

        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.Authority = GetVarOrThrow("AUTH0_DOMAIN");
                options.Audience = GetVarOrThrow("AUTH0_AUDIENCE");
            });
            /*
            services.AddAuthentication()
            .AddJwtBearer(options =>
            {
                options.Authority = GetVarOrThrow("AUTH0_DOMAIN");
                options.Audience = GetVarOrThrow("AUTH0_AUDIENCE");
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        // If the request is for our hub...
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/hubs")))
                        {
                            // Read the token out of the query string
                            context.Token = accessToken;
                        }
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        // Add the access_token as a claim, as we may actually need it
                        var accessToken = context.SecurityToken as JwtSecurityToken;
                        ClaimsIdentity identity = context.Principal.Identity as ClaimsIdentity;
                        if (!identity.HasClaim("email_verified", "true"))
                        {
                            context.Fail("Email address is not validated");
                        }
                        if (accessToken != null)
                        {
                            if (identity != null)
                            {
                                identity.AddClaim(new Claim("access_token", accessToken.RawData));
                            }
                        }

                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                };
            })
            // Om nom nom
            .AddCookie(options => {
                options.ExpireTimeSpan = TimeSpan.FromDays(365);
                options.LoginPath = "/Account/Login/";
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("Authenticated",
                    policy => policy
                        .AddAuthenticationSchemes(
                            JwtBearerDefaults.AuthenticationScheme,
                            CookieAuthenticationDefaults.AuthenticationScheme
                        ).RequireAuthenticatedUser()
                );
            });

            */
            return services;
        }



    }
}
