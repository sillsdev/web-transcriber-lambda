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
using SIL.Paratext.Models;
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
                options.DefaultPageSize = 0;
                //options.EnableOperations = true;
                options.BuildResourceGraph((builder) => {
                    builder.AddResource<DataChanges>();
                });
            });

            services.AddHttpContextAccessor();

            // Add service / repository overrides
            services.AddScoped<IEntityRepository<Group>, GroupRepository>();
            services.AddScoped<IEntityRepository<GroupMembership>, GroupMembershipRepository>();
            services.AddScoped<IEntityRepository<Invitation>, InvitationRepository>();
            services.AddScoped<IEntityRepository<Mediafile>, MediafileRepository>();
            services.AddScoped<IEntityRepository<Organization>, OrganizationRepository>();
            services.AddScoped<IEntityRepository<OrganizationMembership>, OrganizationMembershipRepository>();
            services.AddScoped<IEntityRepository<Passage>, PassageRepository>();
            services.AddScoped<IEntityRepository<PassageSection>, PassageSectionRepository>();
            services.AddScoped<IEntityRepository<Plan>, PlanRepository>();
            services.AddScoped<IEntityRepository<Project>, ProjectRepository>();
            services.AddScoped<IEntityRepository<ProjectIntegration>, ProjectIntegrationRepository>();
            services.AddScoped<IEntityRepository<Section>, SectionRepository>();
            services.AddScoped<IEntityRepository<User>, UserRepository>();
            services.AddScoped<IUpdateService<Project, int>, ProjectService>();

            // services
            services.AddScoped<IResourceService<Activitystate>, ActivitystateService>();
            services.AddScoped<IResourceService<GroupMembership>, GroupMembershipService>();
            services.AddScoped<IResourceService<Group>, GroupService>();
            services.AddScoped<IResourceService<Integration>, IntegrationService>();
            services.AddScoped<IResourceService<Invitation>, InvitationService>();
            services.AddScoped<IResourceService<Mediafile>, MediafileService>();
            services.AddScoped<IResourceService<OrganizationMembership>, OrganizationMembershipService>();
            services.AddScoped<IResourceService<Organization>, OrganizationService>();
            services.AddScoped<IResourceService<ParatextToken>, ParatextTokenService>();
            services.AddScoped<IResourceService<Passage>, PassageService>();
            services.AddScoped<IResourceService<PassageSection>, PassageSectionService>();
            services.AddScoped<IResourceService<Plan>, PlanService>();
            services.AddScoped<IResourceService<Project>, ProjectService>();
            services.AddScoped<IResourceService<ProjectIntegration>, ProjectIntegrationService>();
            services.AddScoped<IResourceService<Section>, SectionService>();
            services.AddScoped<IResourceService<User>, UserService>();
            services.AddScoped<IResourceService<VwPassageStateHistoryEmail>, VwPassageStateHistoryEmailService>();
            //services.AddScoped<IResourceService<OrganizationMembershipInvite>, OrganizationMembershipInviteService>();
            services.AddScoped<IS3Service, S3Service>();
            services.AddScoped<IParatextService, ParatextService>();


            // EventDispatchers
            services.AddScoped<CurrentUserRepository>();
            services.AddScoped<GroupMembershipRepository>();
            services.AddScoped<GroupRepository>();
            services.AddScoped<InvitationRepository>();
            services.AddScoped<MediafileRepository>();
            services.AddScoped<OrganizationMembershipRepository>();
            services.AddScoped<OrganizationRepository>();
            services.AddScoped<PassageRepository>();
            services.AddScoped<PassageSectionRepository>();
            services.AddScoped<PassageRepository>();
            services.AddScoped<PlanRepository>();
            services.AddScoped<ProjectIntegrationRepository>();
            services.AddScoped<ProjectRepository>();
            services.AddScoped<SectionRepository>();
            services.AddScoped<UserRepository>();

            services.AddScoped<ActivitystateService>();
            services.AddScoped<GroupMembershipService>();
            services.AddScoped<GroupService>();
            services.AddScoped<IntegrationService>();
            services.AddScoped<InvitationService>();
            services.AddScoped<MediafileService>();
            services.AddScoped<OrganizationMembershipService>();
            services.AddScoped<OrganizationService>();
            services.AddScoped<ParatextTokenService>();
            services.AddScoped<PassageSectionService>();
            services.AddScoped<PassageService>();
            services.AddScoped<PlanService>();
            services.AddScoped<ProjectIntegrationService>();
            services.AddScoped<ProjectService>();
            services.AddScoped<SectionService>();
            services.AddScoped<UserService>();
            services.AddScoped<VwPassageStateHistoryEmailService>();

            services.AddScoped<Auth0ManagementApiTokenService>();
            services.AddScoped<OrganizationMembershipService>();

            return services;
        }

        public static IServiceCollection AddContextServices(this IServiceCollection services)
        {
            services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

            return services;
        }

        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
        {
            /*( services.AddAuthentication(options =>
             {
                 options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                 options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
             }).AddJwtBearer(options =>
             {
                 options.Authority = GetVarOrThrow("SIL_TR_AUTH0_DOMAIN");
                 options.Audience = GetVarOrThrow("SIL_TR_AUTH0_AUDIENCE");
             });
             */
            services.AddAuthentication()
            .AddJwtBearer(options =>
            {
                options.Authority = GetVarOrThrow("SIL_TR_AUTH0_DOMAIN");
                options.Audience = GetVarOrThrow("SIL_TR_AUTH0_AUDIENCE");
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.Events = new JwtBearerEvents
                {/*
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        // If the request is for our hub...
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) ) && (path.StartsWithSegments("/hubs")))
                        {
                            // Read the token out of the query string
                            context.Token = accessToken;
                        }
                        return System.Threading.Tasks.Task.CompletedTask;
                    }, */
                    OnTokenValidated = context =>
                    {
                        // Add the access_token as a claim, as we may actually need it
                        var accessToken = context.SecurityToken as JwtSecurityToken;
                        ClaimsIdentity identity = context.Principal.Identity as ClaimsIdentity;
                        //SJH 05/01/2019 ours is always false?...
                        //if (!identity.HasClaim("email_verified", "true"))
                        //{
                        //    context.Fail("Email address is not validated");
                        //}
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

            
            return services;
        }



    }
}
