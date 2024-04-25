using Amazon.S3;
using Amazon.SQS;
using JsonApiDotNetCore.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SIL.Logging.Repositories;
using SIL.Transcriber.Data;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

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
            services.AddDbContext<AppDbContext>(options => {
                options.UseNpgsql(GetConnectionString());
            });
            services.AddDbContext<LoggingDbContext>(options => {
                options.UseNpgsql(GetConnectionString());
            });
            // Add JsonApiDotNetCore services.
            services.AddJsonApi<AppDbContext>(
                options => {
                    options.DefaultPageSize = null;
                    options.Namespace = "api";
                    options.UseRelativeLinks = true;
                    options.IncludeTotalResourceCount = false;
                    options.SerializerOptions.WriteIndented = false;
                    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.ResourceLinks = JsonApiDotNetCore.Resources.Annotations.LinkTypes.None;
                    options.RelationshipLinks = JsonApiDotNetCore
                        .Resources
                        .Annotations
                        .LinkTypes
                        .None;
                    options.TopLevelLinks = JsonApiDotNetCore.Resources.Annotations.LinkTypes.None;
                    options.AllowUnknownQueryStringParameters = true;
                    options.AllowUnknownFieldsInRequestBody = true;
                    options.MaximumIncludeDepth = 2;
                    options.EnableLegacyFilterNotation = true;
#if DEBUG
                    options.IncludeExceptionStackTraceInErrors = true;
                    options.IncludeRequestBodyInErrors = true;
#endif
                },
                discovery => discovery.AddCurrentAssembly()
            );
            services.RegisterRepositories();
            services.RegisterServices();
            services.AddSingleton<IS3Service, S3Service>();
            services.AddSingleton<ISQSService, SQSService>();
            services.AddAWSService<IAmazonS3>();
            services.AddAWSService<IAmazonSQS>();
            return services;
        }

        public static void RegisterServices(this IServiceCollection services)
        {
            services.AddScoped<ArtifactCategoryService>();
            services.AddScoped<ArtifactTypeService>();
            services.AddScoped<BibleService>();
            services.AddScoped<CommentService>();
            services.AddScoped<CurrentversionService>();
            services.AddScoped<DataChangeService>();
            services.AddScoped<DiscussionService>();
            services.AddScoped<GraphicService>();
            services.AddScoped<GroupMembershipService>();
            services.AddScoped<GroupService>();
            services.AddScoped<IntegrationService>();
            services.AddScoped<IntellectualPropertyService>();
            services.AddScoped<InvitationService>();
            services.AddScoped<MediafileService>();
            services.AddScoped<IOfflineDataService, OfflineDataService>();
            services.AddScoped<OrganizationBibleService>();
            services.AddScoped<OrganizationMembershipService>();
            services.AddScoped<OrganizationService>();
            services.AddScoped<OrgDataService>();
            services.AddScoped<OrgKeytermService>();
            services.AddScoped<OrgKeytermReferenceService>();
            services.AddScoped<OrgKeytermTargetService>();
            services.AddScoped<OrgWorkflowStepService>();
            services.AddScoped<IParatextService, ParatextService>();
            services.AddScoped<ParatextSyncPassageService>();
            services.AddScoped<ParatextSyncService>();
            services.AddScoped<ParatextTokenHistoryService>();
            services.AddScoped<PassageService>();
            services.AddScoped<PassageStateChangeService>();
            services.AddScoped<PassagetypeService>();
            services.AddScoped<PlanService>();
            services.AddScoped<ProjectIntegrationService>();
            services.AddScoped<ProjectService>();
            services.AddScoped<SectionService>();
            services.AddScoped<SectionPassageService>();
            services.AddScoped<SectionResourceService>();
            services.AddScoped<SectionResourceUserService>();
            services.AddScoped<SharedResourceService>();
            services.AddScoped<SharedResourceReferenceService>();
            services.AddScoped<UserService>();
            services.AddScoped<UserVersionService>();
            services.AddScoped<StatehistoryService>();
            services.AddScoped<VWChecksumService>();
            services.AddScoped<WorkflowStepService>();
        }

        public static void RegisterRepositories(this IServiceCollection services)
        {
            services.AddScoped<ActivitystateRepository>();
            services.AddScoped<ArtifactCategoryRepository>();
            services.AddScoped<ArtifactTypeRepository>();
            services.AddScoped<BibleRepository>();
            services.AddScoped<CommentRepository>();
            services.AddScoped<CurrentUserRepository>();
            services.AddScoped<CurrentversionRepository>();
            services.AddScoped<DashboardRepository>();
            services.AddScoped<DatachangesRepository>();
            services.AddScoped<DiscussionRepository>();
            services.AddScoped<GraphicRepository>();
            services.AddScoped<GroupMembershipRepository>();
            services.AddScoped<GroupRepository>();
            services.AddScoped<IntegrationRepository>();
            services.AddScoped<IntellectualPropertyRepository>();
            services.AddScoped<InvitationRepository>();
            services.AddScoped<MediafileRepository>();
            services.AddScoped<OrganizationBibleRepository>();
            services.AddScoped<OrganizationMembershipRepository>();
            services.AddScoped<OrganizationRepository>();
            services.AddScoped<OrgDataRepository>();
            services.AddScoped<OrgKeytermRepository>();
            services.AddScoped<OrgKeytermReferenceRepository>();
            services.AddScoped<OrgKeytermTargetRepository>();
            services.AddScoped<OrgWorkflowStepRepository>();
            services.AddScoped<ParatextSyncRepository>();
            services.AddScoped<ParatextSyncPassageRepository>();
            services.AddScoped<ParatextTokenRepository>();
            services.AddScoped<ParatextTokenHistoryRepository>();
            services.AddScoped<PassageRepository>();
            services.AddScoped<PassagetypeRepository>();
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
            services.AddScoped<SharedResourceRepository>();
            services.AddScoped<SharedResourceReferenceRepository>();
            services.AddScoped<UserRepository>();
            services.AddScoped<UserVersionRepository>();
            services.AddScoped<StatehistoryRepository>();
            services.AddScoped<VWChecksumRepository>();
            services.AddScoped<WorkflowStepRepository>();
        }

        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
        {
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => {
                    options.Authority = GetVarOrThrow("SIL_TR_AUTH0_DOMAIN");
                    options.Audience = GetVarOrThrow("SIL_TR_AUTH0_AUDIENCE");
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context => {
                            //string TYPE_NAME_IDENTIFIER = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
                            string TYPE_NAME_EMAILVERIFIED = "https://sil.org/email_verified";
                            // Add the access_token as a claim, as we may actually need it
                            ClaimsIdentity? identity = (ClaimsIdentity?)context.Principal?.Identity;
                            if ((!identity?.HasClaim(TYPE_NAME_EMAILVERIFIED, "true") ?? true)
                                && context.HttpContext.Request.Path.Value?.IndexOf("auth/resend") < 0)
                            {
                                Exception ex = new("Email address is not validated."); //this message gets lost
                                ex.Data.Add(402, "Email address is not validated."); //this also gets lost
                                context.Fail(ex);
                                //return System.Threading.Tasks.Task.FromException(ex); //this causes bad things
                            }
                            if (context.SecurityToken is JwtSecurityToken accessToken)
                            {
                                identity?.AddClaim(new Claim("access_token", accessToken.RawData));
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


            services.AddAuthorization(options => {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                  .RequireAuthenticatedUser()
                  .Build();

            });

            return services;
        }

        public static IServiceCollection AddSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(options => {
                options.SwaggerDoc(
                    "v1",
                    new OpenApiInfo
                    {
                        Version = "v2.16.6",
                        Title = "Transcriber API",
                        Contact = new OpenApiContact
                        {
                            Name = "Sara Hentzel",
                            Email = "sara_hentzel@sil.org",
                        },
                    }
                );
                options.AddSecurityDefinition(
                    "Bearer",
                    new OpenApiSecurityScheme()
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "Bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Description =
                            "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\"",
                    }
                );
                options.AddSecurityRequirement(
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    }
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
            return GetVarOrDefault("SIL_TR_CONNECTIONSTRING", "");
        }
    }
}
