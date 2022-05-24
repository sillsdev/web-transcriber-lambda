using SIL.Transcriber.Data;
using JsonApiDotNetCore.Configuration;
using Microsoft.EntityFrameworkCore;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.Cookies;
using SIL.Transcriber.Services;
using SIL.Transcriber.Repositories;
using SIL.Logging.Repositories;
using Amazon.S3;
using JsonApiDotNetCore.Serialization.Response;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using JsonApiDotNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;


namespace SIL.Transcriber
{
    public static class BackendServiceExtension
    {
        public static object AddApiServices(this IServiceCollection services)
        {
            // Add services to the container.
            services.AddHttpContextAccessor();
            services.AddSingleton<IAuthService, AuthService>();

            services.AddScoped<AppDbContextResolver>();
            services.AddScoped<LoggingDbContextResolver>();

            // Add the Entity Framework Core DbContext like you normally would.
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(GetConnectionString());
            });
            services.AddDbContext<LoggingDbContext>(options =>
            {
                options.UseNpgsql(GetConnectionString());
            });
            // Add JsonApiDotNetCore services.
            services.AddJsonApi<AppDbContext>(options =>
            {
                options.DefaultPageSize = null;
                options.Namespace = "api";
                options.UseRelativeLinks = true;
                options.IncludeTotalResourceCount = true;
                options.SerializerOptions.WriteIndented = true;
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.ResourceLinks = JsonApiDotNetCore.Resources.Annotations.LinkTypes.None;
                options.RelationshipLinks = JsonApiDotNetCore.Resources.Annotations.LinkTypes.None;
                options.TopLevelLinks = JsonApiDotNetCore.Resources.Annotations.LinkTypes.None;
                options.AllowUnknownQueryStringParameters = true;
                options.AllowUnknownFieldsInRequestBody = true;
                options.MaximumIncludeDepth = 2;
                options.EnableLegacyFilterNotation = true;
#if DEBUG
                options.IncludeExceptionStackTraceInErrors = true;
                options.IncludeRequestBodyInErrors = true;
#endif
            }, discovery => discovery.AddCurrentAssembly());
            /*services.AddJsonApi(options =>
            {
                //options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                //options.SerializerSettings.MaxDepth = 2;
                //options.SerializerSettings.ContractResolver = new DefaultContractResolver
                //{
                //    NamingStrategy = new KebabCaseNamingStrategy() //TODO
                //};
                //options.SerializerSettings.Converters.Add(new JsonStringEnumConverter());
                //options.EnableOperations = true;

            },
            discovery => discovery.AddCurrentAssembly(), null, services.AddMvcCore()); */

            services.RegisterRepositories();
            services.RegisterServices();

            services.AddSingleton<IS3Service, S3Service>();
            services.AddAWSService<IAmazonS3>();
            return services;
        }
        public static void RegisterServices(this IServiceCollection services)
        {
            services.AddScoped<ArtifactCategoryService>();
            services.AddScoped<ArtifactTypeService>();
            services.AddScoped<CommentService>();
            services.AddScoped<CurrentVersionService>();
            services.AddScoped<DataChangeService>();
            services.AddScoped<DiscussionService>();
            //services.AddScoped<FileResponseService>();
            services.AddScoped<GroupMembershipService>();
            services.AddScoped<GroupService>();
            services.AddScoped<IntegrationService>();
            services.AddScoped<InvitationService>();
            services.AddScoped<MediafileService>();
            services.AddScoped<OfflineDataService>();
            services.AddScoped<OrganizationMembershipService>();
            services.AddScoped<OrganizationService>();
            services.AddScoped<OrgDataService>();
            services.AddScoped<OrgWorkflowStepService>();
            services.AddScoped<ParatextSyncPassageService>();
            services.AddScoped<ParatextSyncService>();
            services.AddScoped<ParatextTokenService>();
            services.AddScoped<ParatextTokenHistoryService>();
            services.AddScoped<PassageService>();
            services.AddScoped<PassageStateChangeService>();
            services.AddScoped<PlanService>();
            services.AddScoped<ProjectIntegrationService>();
            services.AddScoped<ProjectService>();
            services.AddScoped<SectionService>();
            services.AddScoped<SectionPassageService>();
            services.AddScoped<SectionResourceService>();
            services.AddScoped<SectionResourceUserService>();
            services.AddScoped<UserService>();
            services.AddScoped<UserVersionService>();
            services.AddScoped<VwPassageStateHistoryEmailService>();
            services.AddScoped<WorkflowStepService>();
            services.AddScoped<JsonApiWriter>();
        }
        public static void RegisterRepositories(this IServiceCollection services)
        {
            services.AddScoped<ActivitystateRepository>();
            services.AddScoped<ArtifactCategoryRepository>();
            services.AddScoped<ArtifactTypeRepository>();
            services.AddScoped<CommentRepository>();
            services.AddScoped<CurrentUserRepository>();
            services.AddScoped<CurrentVersionRepository>();
            services.AddScoped<DashboardRepository>();
            services.AddScoped<DataChangesRepository>();
            services.AddScoped<DiscussionRepository>();
            //services.AddScoped<FileResponseRepository>();
            services.AddScoped<GroupMembershipRepository>();
            services.AddScoped<GroupRepository>();
            services.AddScoped<IntegrationRepository>();
            services.AddScoped<InvitationRepository>();
            services.AddScoped<MediafileRepository>();
            services.AddScoped<OrganizationMembershipRepository>();
            services.AddScoped<OrganizationRepository>();
            services.AddScoped<OrgDataRepository>();
            services.AddScoped<OrgWorkflowStepRepository>();
            services.AddScoped<ParatextSyncRepository>();
            services.AddScoped<ParatextSyncPassageRepository>();
            services.AddScoped<ParatextTokenRepository>();
            services.AddScoped<ParatextTokenHistoryRepository>();
            services.AddScoped<PassageRepository>();
            services.AddScoped<PassageStateChangeRepository>();
            services.AddScoped<PlanRepository>();
            services.AddScoped<ProjDataRepository>();
            services.AddScoped<PlanTypeRepository>();
            services.AddScoped<ProjectIntegrationRepository>();
            services.AddScoped<ProjectRepository>();
            services.AddScoped<ProjectTypeRepository>();
            services.AddScoped<ResourceRepository>();
            services.AddScoped<RoleRepository>();
            services.AddScoped<SectionRepository>();
            services.AddScoped<SectionPassageRepository>();
            services.AddScoped<SectionResourceRepository>();
            services.AddScoped<SectionResourceUserRepository>();
            services.AddScoped<UserRepository>();
            services.AddScoped<UserVersionRepository>();
            services.AddScoped<VwPassageStateHistoryEmailRepository>();
            services.AddScoped<WorkflowStepRepository>();
        }
        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
        {

            _ = services.AddAuthentication()
            .AddJwtBearer(options =>
            {
                options.Authority = GetVarOrThrow("SIL_TR_AUTH0_DOMAIN");
                options.Audience = GetVarOrThrow("SIL_TR_AUTH0_AUDIENCE");
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        //string TYPE_NAME_IDENTIFIER = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
                        string TYPE_NAME_EMAILVERIFIED = "https://sil.org/email_verified";
                        // Add the access_token as a claim, as we may actually need it
                        ClaimsIdentity? identity = (ClaimsIdentity?)context.Principal?.Identity;
                        if ((!identity?.HasClaim(TYPE_NAME_EMAILVERIFIED, "true") ?? true) && context.HttpContext.Request.Path.Value?.IndexOf("auth/resend") < 0)
                        {
                            Exception ex = new("Email address is not validated."); //this message gets lost
                            ex.Data.Add(402, "Email address is not validated."); //this also gets lost
                            context.Fail(ex);
                            //return System.Threading.Tasks.Task.FromException(ex); //this causes bad things
                        }
                        if (context.SecurityToken is JwtSecurityToken accessToken)
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
            .AddCookie(options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromDays(365);
                options.LoginPath = "/Account/Login/";
            });
            //This is used for calling from auth0 rules (and possibly other outside entities)  We'll use this if we push user changes from auth0
            /* 05/12
            .AddBasic(options =>
            {
                options.Events = new BasicAuthenticationEvents
                {
                    OnValidateCredentials = context =>
                    {
                        IAuthService authService = context.HttpContext.RequestServices.GetService<IAuthService>();
                        if (authService.ValidateWebhookCredentials(context.Username, context.Password))
                        {
                            Claim[] claims = new[]
                            {
                                    new Claim(ClaimTypes.NameIdentifier, context.Username, ClaimValueTypes.String,
                                        context.Options.ClaimsIssuer),
                                    new Claim(ClaimTypes.Name, context.Username, ClaimValueTypes.String,
                                        context.Options.ClaimsIssuer)
                                };

                            context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims,
                                context.Scheme.Name));
                            context.Success();
                        }
                        return Task.CompletedTask;
                    }
                };
            }); */


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
        public static IServiceCollection AddContextServices(this IServiceCollection services)
        {
            services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

            return services;
        }

        private static string GetConnectionString()
        {
            return GetVarOrDefault("SIL_TR_CONNECTIONSTRING","");
        }
    }
}
