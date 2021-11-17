using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using idunno.Authentication.Basic;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIL.Logging.Models;
using SIL.Logging.Repositories;
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
            services.AddScoped<AppDbContextResolver>();
            services.AddScoped<LoggingDbContextResolver>();

            // add jsonapi dotnet core
            // - includes IHttpContextAccessor as a singleton
            services.AddJsonApi(options =>
            {
                options.Namespace = "api";
                options.IncludeTotalRecordCount = false;
                options.DefaultPageSize = 0;
                options.AllowCustomQueryParameters = true;
                //options.EnableOperations = true;
                options.BuildResourceGraph((builder) =>
                {
                    builder.AddDbContext<AppDbContext>();
                    builder.AddDbContext<LoggingDbContext>();
                    //builder.AddResource<Dashboard>();
                    //builder.AddResource<DataChanges>();
                    builder.AddResource<FileResponse>();
                });
            }, services.AddMvcCore());

            services.AddHttpContextAccessor();

            // Add service / repository overrides
            services.AddScoped<IEntityRepository<Comment>, CommentRepository>();
            services.AddScoped<IEntityRepository<CurrentVersion>, CurrentVersionRepository>();
            services.AddScoped<IEntityRepository<Dashboard>, DashboardRepository>();
            services.AddScoped<IEntityRepository<DataChanges>, DataChangesRepository>();
            services.AddScoped<IEntityRepository<Discussion>, DiscussionRepository>();
            services.AddScoped<AppDbContextRepository<FileResponse>, FileResponseRepository>();
            services.AddScoped<IEntityRepository<Group>, GroupRepository>();
            services.AddScoped<IEntityRepository<GroupMembership>, GroupMembershipRepository>();
            services.AddScoped<IEntityRepository<Invitation>, InvitationRepository>();
            services.AddScoped<IEntityRepository<Mediafile>, MediafileRepository>();
            services.AddScoped<IEntityRepository<Organization>, OrganizationRepository>();
            services.AddScoped<IEntityRepository<OrganizationMembership>, OrganizationMembershipRepository>();
            services.AddScoped<IEntityRepository<ArtifactCategory>, ArtifactCategoryRepository>();
            services.AddScoped<IEntityRepository<ArtifactType>, ArtifactTypeRepository>();
            services.AddScoped<IEntityRepository<OrgData>, OrgDataRepository>();
            services.AddScoped<IEntityRepository<OrgWorkflowStep>, OrgWorkflowStepRepository>();
            services.AddScoped<AppDbContextRepository<ParatextToken>, ParatextTokenRepository>();
            services.AddScoped<IEntityRepository<Passage>, PassageRepository>();
            services.AddScoped<IEntityRepository<PassageStateChange>, PassageStateChangeRepository>();
            services.AddScoped<IEntityRepository<Plan>, PlanRepository>();
            services.AddScoped<IEntityRepository<ProjData>, ProjDataRepository>();
            services.AddScoped<IEntityRepository<Project>, ProjectRepository>();
            services.AddScoped<IEntityRepository<ProjectIntegration>, ProjectIntegrationRepository>();
            services.AddScoped<IEntityRepository<Role>, RoleRepository>();
            services.AddScoped<IEntityRepository<Section>, SectionRepository>();
            services.AddScoped<IEntityRepository<SectionPassage>, SectionPassageRepository>();
            services.AddScoped<IEntityRepository<SectionResource>, SectionResourceRepository>();
            services.AddScoped<IEntityRepository<SectionResourceUser>, SectionResourceUserRepository>();
            services.AddScoped<IEntityRepository<User>, UserRepository>();
            services.AddScoped<IEntityRepository<UserVersion>, UserVersionRepository>();
            services.AddScoped<IEntityRepository<VwPassageStateHistoryEmail>, VwPassageStateHistoryEmailRepository>();
            services.AddScoped<IEntityRepository<WorkflowStep>, WorkflowStepRepository>();

            services.AddScoped<IUpdateService<Project, int>, ProjectService>();

            services.AddScoped<LoggingDbContextRepository<ParatextSync>, ParatextSyncRepository>();
            services.AddScoped<LoggingDbContextRepository<ParatextSyncPassage>, ParatextSyncPassageRepository>();
            services.AddScoped<LoggingDbContextRepository<ParatextTokenHistory>, ParatextTokenHistoryRepository>();
            // services
            services.AddScoped<IResourceService<Activitystate>, ActivitystateService>();
            services.AddScoped<IResourceService<Comment>, CommentService>();
            services.AddScoped<IResourceService<CurrentVersion>, CurrentVersionService>();
            services.AddScoped<IResourceService<DataChanges>, DataChangeService>(); 
            services.AddScoped<IResourceService<Discussion>, DiscussionService>();
            services.AddScoped<IResourceService<FileResponse>, FileResponseService>();
            services.AddScoped<IResourceService<GroupMembership>, GroupMembershipService>();
            services.AddScoped<IResourceService<Group>, GroupService>();
            services.AddScoped<IResourceService<Integration>, IntegrationService>();
            services.AddScoped<IResourceService<Invitation>, InvitationService>();
            services.AddScoped<IResourceService<Mediafile>, MediafileService>();
            services.AddScoped<IResourceService<OrganizationMembership>, OrganizationMembershipService>();
            services.AddScoped<IResourceService<Organization>, OrganizationService>();
            services.AddScoped<IResourceService<ArtifactType>, ArtifactTypeService>();
            services.AddScoped<IResourceService<ArtifactCategory>, ArtifactCategoryService>();
            services.AddScoped<IResourceService<OrgWorkflowStep>, OrgWorkflowStepService>();
            services.AddScoped<IResourceService<ParatextToken>, ParatextTokenService>();
            services.AddScoped<IResourceService<ParatextTokenHistory>, ParatextTokenHistoryService>();
            services.AddScoped<IResourceService<Passage>, PassageService>();
            services.AddScoped<IResourceService<PassageStateChange>, PassageStateChangeService>();
            services.AddScoped<IResourceService<Plan>, PlanService>();
            services.AddScoped<IResourceService<Project>, ProjectService>();
            services.AddScoped<IResourceService<ProjectIntegration>, ProjectIntegrationService>();
            services.AddScoped<IResourceService<Section>, SectionService>();
            services.AddScoped<IResourceService<SectionResource>, SectionResourceService>();
            services.AddScoped<IResourceService<SectionResourceUser>, SectionResourceUserService>(); 
            services.AddScoped<IResourceService<User>, UserService>();
            services.AddScoped<IResourceService<UserVersion>, UserVersionService>();
            services.AddScoped<IResourceService<VwPassageStateHistoryEmail>, VwPassageStateHistoryEmailService>();
            services.AddScoped<IResourceService<WorkflowStep>, WorkflowStepService>();
            //services.AddScoped<IResourceService<OrganizationMembershipInvite>, OrganizationMembershipInviteService>();
            services.AddScoped<IS3Service, S3Service>();
            services.AddScoped<IOfflineDataService, OfflineDataService>();
            services.AddScoped<OrgDataService, OrgDataService>();
            services.AddScoped<IParatextService, ParatextService>();
            services.AddScoped<SectionPassageService, SectionPassageService>();

            services.AddScoped<ParatextSyncRepository>();
            services.AddScoped<ParatextSyncPassageRepository>();


            // EventDispatchers
            services.AddScoped<CommentRepository>();
            services.AddScoped<CurrentVersionRepository>();
            services.AddScoped<DashboardRepository>();
            services.AddScoped<DataChangesRepository>();
            services.AddScoped<DiscussionRepository>();
            services.AddScoped<CurrentUserRepository>();
            services.AddScoped<FileResponseRepository>();
            services.AddScoped<GroupMembershipRepository>();
            services.AddScoped<GroupRepository>();
            services.AddScoped<InvitationRepository>();
            services.AddScoped<MediafileRepository>();
            services.AddScoped<OrganizationMembershipRepository>();
            services.AddScoped<OrganizationRepository>();
            services.AddScoped<ArtifactCategoryRepository>();
            services.AddScoped<ArtifactTypeRepository>();
            services.AddScoped<OrgDataRepository>();
            services.AddScoped<OrgWorkflowStepRepository>();
            services.AddScoped<ParatextTokenRepository>();
            services.AddScoped<PassageRepository>();
            services.AddScoped<PassageStateChangeRepository>();
            services.AddScoped<PlanRepository>();
            services.AddScoped<ProjDataRepository>();
            services.AddScoped<ProjectIntegrationRepository>();
            services.AddScoped<ProjectRepository>();
            services.AddScoped<RoleRepository>();
            services.AddScoped<VwPassageStateHistoryEmailRepository>();
            services.AddScoped<WorkflowStepRepository>();
            services.AddScoped<SectionRepository>();
            services.AddScoped<SectionPassageRepository>();
            services.AddScoped<SectionResourceRepository>();
            services.AddScoped<SectionResourceUserRepository>();
            services.AddScoped<UserRepository>();
            services.AddScoped<UserVersionRepository>();
            services.AddScoped<ParatextSyncRepository>();
            services.AddScoped<ParatextSyncPassageRepository>();
            services.AddScoped<ParatextTokenHistoryRepository>();

            services.AddScoped<ActivitystateService>();
            services.AddScoped<CommentService>();
            services.AddScoped<CurrentVersionService>();
            services.AddScoped<DataChangeService>();
            services.AddScoped<DiscussionService>();
            services.AddScoped<FileResponseService>();
            services.AddScoped<GroupMembershipService>();
            services.AddScoped<GroupService>();
            services.AddScoped<IntegrationService>();
            services.AddScoped<InvitationService>();
            services.AddScoped<MediafileService>();
            services.AddScoped<OfflineDataService>();
            services.AddScoped<OrganizationMembershipService>();
            services.AddScoped<OrganizationService>();
            services.AddScoped<ArtifactCategoryService>();
            services.AddScoped<ArtifactTypeService>();
            services.AddScoped<OrgDataService>();
            services.AddScoped<OrgWorkflowStepService>();
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

            services.AddSingleton<IAuthService, AuthService>();
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
                {
                    OnTokenValidated = context =>
                    {
                        //string TYPE_NAME_IDENTIFIER = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
                        string TYPE_NAME_EMAILVERIFIED = "https://sil.org/email_verified";
                        // Add the access_token as a claim, as we may actually need it
                        JwtSecurityToken accessToken = context.SecurityToken as JwtSecurityToken;
                        ClaimsIdentity identity = context.Principal.Identity as ClaimsIdentity;
                        if (!identity.HasClaim(TYPE_NAME_EMAILVERIFIED, "true") && context.HttpContext.Request.Path.Value.IndexOf("auth/resend") < 0)
                        {
                            Exception ex = new Exception("Email address is not validated."); //this message gets lost
                            ex.Data.Add(402, "Email address is not validated."); //this also gets lost
                            context.Fail(ex);
                            //return System.Threading.Tasks.Task.FromException(ex); //this causes bad things
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
            })
            //This is used for calling from auth0 rules (and possibly other outside entities)  We'll use this if we push user changes from auth0
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
