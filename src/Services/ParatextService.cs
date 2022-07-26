using IdentityModel;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json.Linq;
using SIL.Logging.Models;
using SIL.Logging.Repositories;
using SIL.Paratext.Models;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services
{
    /// <summary>Exchanges data with PT projects in the cloud.</summary>
    public class ParatextService : IParatextService
    {
        protected ICurrentUserContext CurrentUserContext;
        protected readonly AppDbContext dbContext;
        protected readonly LoggingDbContext logDbContext;

        private readonly HttpClientHandler _httpClientHandler;
        private readonly HttpClient _dataAccessClient;
        private readonly HttpClient _registryClient;

        private readonly MediafileService MediafileService;
        private readonly PassageService PassageService;
        private readonly SectionService SectionService;
        private readonly ProjectService ProjectService;
        public CurrentUserRepository CurrentUserRepository { get; }

        readonly private HttpContext? HttpContext;
        protected ILogger<ParatextService> Logger { get; set; }

        private readonly ProjectIntegrationRepository ProjectIntegrationRepository;

        public ParatextService(
            AppDbContextResolver contextResolver,
            LoggingDbContextResolver logContextResolver,
            IWebHostEnvironment env,
            IHttpContextAccessor httpContextAccessor,
            ICurrentUserContext currentUserContext,
            PassageService passageService,
            MediafileService mediafileService,
            SectionService sectionService,
            ProjectService projectService,
            CurrentUserRepository currentUserRepository,
            ProjectIntegrationRepository piRepo,
            ILoggerFactory loggerFactory
        )
        {
            dbContext = (AppDbContext)contextResolver.GetContext();
            logDbContext = (LoggingDbContext)logContextResolver.GetContext();
            HttpContext = httpContextAccessor.HttpContext;
            PassageService = passageService;
            MediafileService = mediafileService;
            SectionService = sectionService;
            ProjectService = projectService;
            CurrentUserContext = currentUserContext;
            CurrentUserRepository = currentUserRepository;
            ProjectIntegrationRepository = piRepo;
            Logger = loggerFactory.CreateLogger<ParatextService>();

            _httpClientHandler = new HttpClientHandler();
            _dataAccessClient = new HttpClient(_httpClientHandler);
            _registryClient = new HttpClient(_httpClientHandler);
            if (env.IsDevelopment() || env.IsStaging() || env.IsEnvironment("Testing"))
            {
                _httpClientHandler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            _dataAccessClient.BaseAddress = new Uri(
                GetVarOrDefault("SIL_TR_PARATEXT_DATA", "https://data-access.paratext.org/")
            );
            _registryClient.BaseAddress = new Uri(
                GetVarOrDefault("SIL_TR_PARATEXT_REGISTRY", "https://registry.paratext.org/")
            );
            _registryClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );
        }

        public UserSecret ParatextLogin()
        {
            User? currentUser = CurrentUserRepository.GetCurrentUser();
            if (currentUser == null)
                throw new Exception("Unable to get current user information");
            UserSecret? tokenFromAuth0 = CurrentUserContext.ParatextLogin(
                GetVarOrDefault("SIL_TR_PARATEXT_AUTH0_CONNECTION", "Paratext-Transcriber"),
                currentUser.Id
            );

            if (tokenFromAuth0 == null)
            {
                throw new SecurityException("User is not logged in to Paratext-Transcriber");
            }
            //get existing
            IEnumerable<ParatextToken>? tokens = dbContext.Paratexttokens.Where(
                t => t.UserId == currentUser.Id
            );
            if (tokens != null && tokens.Any())
            {
                ParatextToken savedToken = tokens.First();
                if (
                    tokenFromAuth0.ParatextTokens.IssuedAt > savedToken.IssuedAt
                    //&& newPTToken.ParatextTokens.RefreshToken != null
                )
                {
                    savedToken.AccessToken = tokenFromAuth0.ParatextTokens.AccessToken;
                    savedToken.RefreshToken = tokenFromAuth0.ParatextTokens.RefreshToken ?? "";
                    Logger.LogInformation("new token newer than saved token");
                    _ = dbContext.Paratexttokens.Update(savedToken);
                    dbContext.SaveChanges();
                    //TokenHistoryRepo.CreateAsync(new ParatextTokenHistory(currentUser.Id, token.AccessToken, token.RefreshToken, "From Login"));
                }
                else
                {
                    tokenFromAuth0.ParatextTokens = savedToken;
                    //TokenHistoryRepo.CreateAsync(new ParatextTokenHistory(currentUser.Id, token.AccessToken, token.RefreshToken, "From DB"));
                }
            }
            else
            {
                Logger.LogInformation("no saved token");
                _ = dbContext.Paratexttokens.Add(tokenFromAuth0.ParatextTokens);
                dbContext.SaveChanges();
                //TokenHistoryRepo.CreateAsync(new ParatextTokenHistory(currentUser.Id, newPTToken.ParatextTokens.AccessToken, newPTToken.ParatextTokens.RefreshToken, "From First Login"));
            }
            return tokenFromAuth0;
        }

        private Claim? GetClaim(string AccessToken, string claimtype)
        {
            User? currentUser = CurrentUserRepository.GetCurrentUser();
            JwtSecurityToken accessToken = new (AccessToken);
            Claim? claim = accessToken.Claims.FirstOrDefault(c => c.Type == claimtype);
            //System.Console.WriteLine("XXX CLAIM GetClaim {0}", claim?.ToString() ?? "null");
            return claim;
        }

        public async Task<IReadOnlyList<ParatextOrg>> GetOrgsAsync(UserSecret userSecret)
        {
            _ = VerifyUserSecret(userSecret);
            string response = await CallApiAsync(
                _registryClient,
                userSecret,
                HttpMethod.Get,
                "orgs"
            );
            JArray orgArray = JArray.Parse(response);
            List<ParatextOrg> orgs = new();
            foreach (JObject o in orgArray)
            {
                List<string> domains = new();
                JToken? dsrc = o["domains"];
                if (dsrc != null)
                    foreach (string? d in dsrc)
                        if (d != null)
                            domains.Add(d);
                orgs.Add(
                    new ParatextOrg
                    {
                        Id = (string?)o ["id"] ?? "",
                        Name = (string?)o ["name"] ?? "",
                        NameLocal = (string?)o ["nameLocal"] ?? "",
                        Url = (string?)o ["url"],
                        Abbr = (string?)o ["abbr"],
                        Parent = (string?)o ["parent"],
                        Location = (string?)o ["location"],
                        Area = (string?)o ["area"],
                        Public = (bool)(o ["public"] ?? false),
                        Active = (bool)(o ["active"] ?? false),
                        InDbl = (bool)(o ["in_dbl"] ?? false),
                        AuthorizedForParatext = (bool)(o ["authorizedForParatext"] ?? false),
                        ShareBasicProgressInfo = (bool)(o ["shareBasicProgressInfo"] ?? false),
                        CountryISO = (string?)o ["country_iso"],
                        Domains = domains,
                    }
                );
                ;
            }
            ;
            return orgs;
        }

        public async Task<IReadOnlyList<ParatextProject>?> GetProjectsAsync(UserSecret userSecret)
        {
            _ = VerifyUserSecret(userSecret);
            //Console.WriteLine("GetProjectsAsync");
            Claim? usernameClaim = GetClaim(userSecret.ParatextTokens.AccessToken, "username");
            string? username = usernameClaim?.Value;
            string response = await CallApiAsync(
                _dataAccessClient,
                userSecret,
                HttpMethod.Get,
                "projects"
            );
            XElement reposElem = XElement.Parse(response);
            if (reposElem == null)
                return null;

            List<ParatextProject> projects = new();
            foreach (XElement repoElem in reposElem.Elements("repo"))
            {
                string? projId = (string?)repoElem.Element("projid");
                XElement? userElem = repoElem
                    .Element("users")
                    ?.Elements("user")
                    ?.FirstOrDefault(ue => (string?)ue.Element("name") == username);
                string? role = (string?)userElem?.Element("role");
                IEnumerable<string> projectids = ProjectService
                    .LinkedToParatext(projId ?? "")
                    .Select(p => p.Id.ToString());

                projects.Add(
                    new ParatextProject
                    {
                        ParatextId = projId ?? "",
                        ProjectType = (string?)repoElem.Element("projecttype") ?? "unknown",
                        BaseProject = (string?)repoElem.Element("baseprojid") ?? "",
                        ShortName = (string?)repoElem.Element("proj") ?? "",
                        Name = "",
                        LanguageTag = "",
                        LanguageName = "",
                        ProjectIds = projectids,
                        IsConnectable = (
                            role is ParatextProjectRoles.Administrator
                            or ParatextProjectRoles.Translator
                        ),
                        CurrentUserRole = role,
                    }
                );
            }

            //get more info for those projects that are registered
            response = await CallApiAsync(_registryClient, userSecret, HttpMethod.Get, "projects");
            JArray projectArray = JArray.Parse(response);

            foreach (JToken projectObj in projectArray)
            {
                JToken? identificationObj = projectObj["identification_systemId"]?.FirstOrDefault(
                    id => (string?)id["type"] == "paratext"
                );
                if (identificationObj == null)
                    continue;
                string paratextId = (string?)identificationObj["text"] ?? "";
                ParatextProject? proj = projects.FirstOrDefault(p => p.ParatextId == paratextId);
                if (proj == null)
                    continue;

                string name =
                    (string?)identificationObj["fullname"]
                    ?? (string?)identificationObj["name"]
                    ?? "";
                string langName = (string?)projectObj["language_iso"] ?? "";
                string langTag = (string?)projectObj["language_ldml"] ?? "";
                //if (StandardSubtags.TryGetLanguageFromIso3Code(langName, out LanguageSubtag subtag))
                //   langName = subtag.Name;
                proj.Name = name;
                proj.LanguageName = langName;
                proj.LanguageTag = langTag;
            }

            //now go through them again to link BT to base project
            foreach (ParatextProject proj in projects)
            {
                List<ParatextProject> subProjects = projects.FindAll(
                    p => p.BaseProject == proj.ParatextId
                );
                subProjects.ForEach(sp => {
                    if (sp.Name.Length == 0)
                        sp.Name = sp.ProjectType + " " + proj.Name;
                    if (sp.LanguageName == null)
                        sp.LanguageName = "";
                    sp.LanguageName += (sp.LanguageName.Length > 0 ? "," : "") + proj.LanguageName;
                    sp.LanguageTag += (sp.LanguageTag.Length > 0 ? "," : "") + proj.LanguageTag;
                });
            }

            return projects;
        }

        public async Task<IReadOnlyList<ParatextProject>?> GetProjectsAsync(
            UserSecret userSecret,
            string languageTag
        )
        {
            IReadOnlyList<ParatextProject>? projects = await GetProjectsAsync(userSecret);
            return projects?.Where(p => p.LanguageTag.Split(",").Contains(languageTag)).ToList();
        }

        public async Task<Attempt<string?>> TryGetProjectRoleAsync(
            UserSecret userSecret,
            string paratextId
        )
        {
            _ = VerifyUserSecret(userSecret);
            try
            {
                Claim? subClaim = GetClaim(
                    userSecret.ParatextTokens.AccessToken,
                    JwtClaimTypes.Subject
                );
                if (subClaim != null)
                {
                    string response = await CallApiAsync(
                        _registryClient,
                        userSecret,
                        HttpMethod.Get,
                        $"projects/{paratextId}/members/{subClaim.Value}"
                    );
                    JObject memberObj = JObject.Parse(response);

                    return Attempt.Success((string?)memberObj ["role"]);
                }
                return Attempt.Failure((string?)null, "Claim not found");
            }
            catch (HttpRequestException ex)
            {
                return Attempt.Failure((string?)null, ex.Message);
            }
        }

        public async Task<Attempt<string?>> TryGetUserEmailsAsync(
            UserSecret userSecret,
            string inviteId
        )
        {
            try
            {
                _ = VerifyUserSecret(userSecret);
                if (!int.TryParse(inviteId, out int id))
                    return Attempt.Failure("Invalid invitation Id");

                Invitation? invite = dbContext.Invitations.Find(id);
                if (invite != null)
                {
                    string response = await CallApiAsync(
                        _registryClient,
                        userSecret,
                        HttpMethod.Get,
                        $"users/?emails.address=" + invite.Email
                    );
                    JArray memberObj = JArray.Parse(response);
                    if (
                        memberObj.Count > 0
                        && memberObj [0] ["username"]?.ToString() == GetParatextUsername(userSecret)
                    )
                    {
                        invite.Email = CurrentUserContext.Email;
                        invite.Accepted = true;
                        _ = dbContext.Invitations.Update(invite);
                        _ = dbContext.SaveChanges();
                        return Attempt.Success(inviteId);
                    }
                    else
                        return Attempt.Failure("notfound");
                }
                return Attempt.Failure("notfound");
            }
            catch (HttpRequestException ex)
            {
                return Attempt.Failure((string?)null, ex.Message);
            }
        }

        private static bool VerifyUserSecret(UserSecret userSecret)
        {
            if (userSecret is null || userSecret.ParatextTokens is null)
                throw new SecurityException("Paratext credentials not provided.");
            return userSecret.ParatextTokens.AccessToken is null
                || userSecret.ParatextTokens.AccessToken.Length == 0
                ? throw new SecurityException("Current user is not logged in to Paratext.")
                : true;
        }

        public string? GetParatextUsername(UserSecret userSecret)
        {
            _ = VerifyUserSecret(userSecret);
            Claim? usernameClaim = GetClaim(userSecret.ParatextTokens.AccessToken, "username");
            return usernameClaim?.Value;
        }

        public async Task<IReadOnlyList<string>?> GetBooksAsync(
            UserSecret userSecret,
            string projectId
        )
        {
            _ = VerifyUserSecret(userSecret);
            string response = await CallApiAsync(
                _dataAccessClient,
                userSecret,
                HttpMethod.Get,
                $"books/{projectId}"
            );
            XElement books = XElement.Parse(response);
            string[]? bookIds = books
                .Elements("Book")
                ?.Select(b => (string?)b.Attribute("id") ?? "")
                ?.ToArray();
            return bookIds;
        }

        public Task<string> GetBookTextAsync(UserSecret userSecret, string projectId, string bookId)
        {
            _ = VerifyUserSecret(userSecret);
            return CallApiAsync(
                _dataAccessClient,
                userSecret,
                HttpMethod.Get,
                $"text/{projectId}/{bookId}"
            );
        }

        public Task<string> GetChapterTextAsync(
            UserSecret userSecret,
            string projectId,
            string bookId,
            int chapter
        )
        {
            _ = VerifyUserSecret(userSecret);
            Console.WriteLine($"text/{projectId}/{bookId}/{chapter}");
            return CallApiAsync(
                _dataAccessClient,
                userSecret,
                HttpMethod.Get,
                $"text/{projectId}/{bookId}/{chapter}"
            );
        }

        /// <summary>Update cloud with new edits in usxText and return the combined result.</summary>
        public Task<string> UpdateChapterTextAsync(
            UserSecret userSecret,
            string projectId,
            string bookId,
            int chapter,
            string revision,
            string usxText
        )
        {
            _ = VerifyUserSecret(userSecret);
            return CallApiAsync(
                _dataAccessClient,
                userSecret,
                HttpMethod.Post,
                $"text/{projectId}/{revision}/{bookId}/{chapter}",
                usxText
            );
        }

        /// <summary>Update cloud with new edits in usxText and return the combined result.</summary>
        public Task<string> UpdateBookTextAsync(
            UserSecret userSecret,
            string projectId,
            string bookId,
            string revision,
            string usxText
        )
        {
            _ = VerifyUserSecret(userSecret);
            return CallApiAsync(
                _dataAccessClient,
                userSecret,
                HttpMethod.Post,
                $"text/{projectId}/{revision}/{bookId}",
                usxText
            );
        }

        public Task<string> GetNotesAsync(UserSecret userSecret, string projectId, string bookId)
        {
            _ = VerifyUserSecret(userSecret);
            return CallApiAsync(
                _dataAccessClient,
                userSecret,
                HttpMethod.Get,
                $"notes/{projectId}/{bookId}"
            );
        }

        public Task<string> UpdateNotesAsync(
            UserSecret userSecret,
            string projectId,
            string notesText
        )
        {
            _ = VerifyUserSecret(userSecret);
            return CallApiAsync(
                _dataAccessClient,
                userSecret,
                HttpMethod.Post,
                $"notes/{projectId}",
                notesText
            );
        }

        private async Task<UserSecret> RefreshAccessTokenAsync(UserSecret userSecret)
        {
            _ = VerifyUserSecret(userSecret);
            if (userSecret.ParatextTokens.RefreshToken == null || userSecret.ParatextTokens.RefreshToken == "")
            {
                throw new SecurityException("401 RefreshTokenNull");
            }
            HttpRequestMessage request = new(HttpMethod.Post, "api8/token");
            JObject requestObj =
                new(
                    new JProperty("grant_type", "refresh_token"),
                    new JProperty("client_id", GetVarOrDefault("SIL_TR_PARATEXT_CLIENT_ID", "")),
                    new JProperty(
                        "client_secret",
                        GetVarOrDefault("SIL_TR_PARATEXT_CLIENT_SECRET", "")
                    ),
                    new JProperty("refresh_token", userSecret.ParatextTokens.RefreshToken)
                );
            request.Content = new StringContent(
                requestObj.ToString(),
                Encoding.UTF8,
                "application/json"
            );
            HttpResponseMessage response = await _registryClient.SendAsync(request);
            string responseJson = await response.Content.ReadAsStringAsync();
            JObject responseObj = JObject.Parse(responseJson);

            //log it
            //requestObj["client_secret"] = "XXX";
            //await TokenHistoryRepo.CreateAsync(new ParatextTokenHistory(userSecret.ParatextTokens.UserId, (string)responseObj["access_token"], (string)responseObj["refresh_token"], requestObj.ToString(), response.ReasonPhrase + responseObj));
            if (responseObj?.Count > 0 && (responseObj ["error_description"]?.ToString()?.Contains("refresh token") ?? false))
                throw new SecurityException("401 RefreshTokenInvalid.  Expected on Dev and QA.  Login again with Paratext connection.");

            _ = response.EnsureSuccessStatusCode();

            userSecret.ParatextTokens.AccessToken = (string?)responseObj?["access_token"] ?? "";
            userSecret.ParatextTokens.RefreshToken = (string?)responseObj?["refresh_token"] ?? "";

            _ = dbContext.Paratexttokens.Update(userSecret.ParatextTokens);

            //log it
            //await TokenHistoryRepo.CreateAsync(new ParatextTokenHistory(userSecret.ParatextTokens.UserId, userSecret.ParatextTokens.AccessToken, userSecret.ParatextTokens.RefreshToken, "AfterRefresh"));

            return userSecret;
        }

        private async Task<string> CallApiAsync(
            HttpClient client,
            UserSecret userSecret,
            HttpMethod method,
            string url,
            string? content = null
        )
        {
            _ = VerifyUserSecret(userSecret);

            bool expired = !userSecret.ParatextTokens.ValidateLifetime();
            bool refreshed = false;
            while (!refreshed)
            {
                if (expired)
                {
                    userSecret = await RefreshAccessTokenAsync(userSecret);
                    refreshed = true;
                }

                HttpRequestMessage request = new (method, $"api8/{url}");
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    userSecret.ParatextTokens.AccessToken
                );
                if (content != null)
                    request.Content = new StringContent(content);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    expired = true;
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException(
                        $"HTTP Request error, Code: {response.StatusCode}, Content: {error}"
                    );
                }
            }

            throw new SecurityException("The current user's Paratext access token is invalid.");
        }

        private async Task<ParatextChapter> GetParatextChapterAsync(
            UserSecret userSecret,
            string paratextId,
            string book,
            int number
        )
        {
            _ = VerifyUserSecret(userSecret);

            ParatextChapter chapter = new()
            {
                Book = book,
                Chapter = number
            };
            //get the text out of paratext
            string bookText = await GetChapterTextAsync(userSecret, paratextId, book, number);
            XElement bookTextElem = XElement.Parse(bookText);
            chapter.Project = (string?)bookTextElem.Attribute("project") ?? "";
            chapter.Revision = (string?)bookTextElem.Attribute("revision") ?? "";
            chapter.OriginalValue = bookTextElem.Value;
            chapter.OriginalUSX = bookTextElem.Element("usx");
            return chapter;
        }

        private class BookChapter : IEquatable<BookChapter>
        {
            public string Book { get; }
            public int Chapter { get; }

            public BookChapter(string? book, int chapter)
            {
                Book = book ?? "";
                Chapter = chapter;
            }
            public override bool Equals(object? obj)
            {
                //   http://go.microsoft.com/fwlink/?LinkID=85237  
                // and also the guidance for operator== at
                //   http://go.microsoft.com/fwlink/?LinkId=85238

                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                return Equals(obj as BookChapter);
            }
            public bool Equals(BookChapter? other)
            {
                //Check whether the compared object is null.
                if (other is null)
                    return false;

                //Check whether the compared object references the same data.
                if (Object.ReferenceEquals(this, other))
                    return true;

                //Check whether the products' properties are equal.
                return Book.Equals(other.Book) && Chapter.Equals(other.Chapter);
            }

            public override int GetHashCode()
            {
                //Get hash code for the Name field if it is not null.
                int hashBook = Book.GetHashCode();

                //Get hash code for the Code field.
                int hashChapter = Chapter.GetHashCode();

                //Calculate the hash code for the product.
                return hashBook ^ hashChapter;
            }

            public override string ToString()
            {
                return Book + ' ' + Chapter;
            }

        }

        private async Task<List<ParatextChapter>> GetPassageChaptersAsync(
            UserSecret userSecret,
            string paratextId,
            IEnumerable<BookChapter> book_chapters
        )
        {
            List<ParatextChapter> chapterList = new();

            foreach (BookChapter bc in book_chapters)
            {
                chapterList.Add(
                    await GetParatextChapterAsync(userSecret, paratextId, bc.Book, bc.Chapter)
                );
            }
            return chapterList;
        }

        private static IEnumerable<BookChapter> BookChapters(IEnumerable<Passage> passages)
        {
            return passages.Select(p => new BookChapter(p.Book, p.StartChapter)).Distinct();
        }

        public async Task<List<ParatextChapter>> GetSectionChaptersAsync(
            UserSecret userSecret,
            int sectionId,
            int typeId
        )
        {
            Artifacttype? type = dbContext.Artifacttypes.Find(typeId);
            string paratextId = ParatextHelpers.ParatextProject(
                SectionService.GetProjectId(sectionId) ?? 0,
                type?.Typename ?? "",
                ProjectIntegrationRepository
            );
            IQueryable<Passage> passages = PassageService.GetBySection(sectionId);
            return await GetPassageChaptersAsync(userSecret, paratextId, BookChapters(passages));
        }

        public int PlanPassagesToSyncCount(int planId, int artifactTypeId)
        {
            return MediafileService.ReadyToSync(planId, artifactTypeId).Count();
        }

        public async Task<string?> PassageTextAsync(int passageId, int typeId)
        {
            IEnumerable<Passage> passages = PassageService.Get(passageId).ToList();
            Passage? passage = passages.FirstOrDefault();
            if (passage == null)
            {
                throw new Exception("Passage not found or user does not have access to passage.");
            }
            Artifacttype? type = dbContext.Artifacttypes.Find(typeId);

            string paratextId = ParatextHelpers.ParatextProject(
                PassageService.GetProjectId(passage),
                type?.Typename ?? "",
                ProjectIntegrationRepository
            );
            UserSecret userSecret = ParatextLogin();
            string err = VerifyReferences(userSecret, passages, paratextId);
            if (err.Length > 0)
                throw new Exception(err);

            //assume startChapter=endChapter for all passages
            IEnumerable<BookChapter> book_chapters = BookChapters(passages);
            List<ParatextChapter> chapterList = await GetPassageChaptersAsync(
                userSecret,
                paratextId,
                book_chapters
            );
            return ParatextHelpers.GetParatextData(chapterList.First()?.OriginalUSX, passage);
        }

        public async Task<int> ProjectPassagesToSyncCountAsync(int projectId, int artifactTypeid)
        {
            Project? project = await ProjectService.GetWithPlansAsync(projectId);
            int total = 0;
            if (project != null)
                foreach (Plan p in project.Plans)
                {
                    total += MediafileService.ReadyToSync(p.Id, artifactTypeid).Count();
                }
            return total;
        }

        public string VerifyReferences(
            UserSecret userSecret,
            IEnumerable<Passage> passages,
            string paratextId
        )
        {
            IReadOnlyList<string>? books = GetBooksAsync(userSecret, paratextId).Result;
            string err = "";
            foreach (Passage p in passages)
            {
                if (p.Book == "")
                    err += string.Format(
                        "||Empty Book|{0}|{1}|",
                        p.Section?.Sequencenum,
                        p.Sequencenum
                    );
                else if (books == null || !books.Contains(p.Book))
                    err += string.Format(
                        "||Missing Book|{0}|{1}|{2}|",
                        p.Section?.Sequencenum,
                        p.Sequencenum,
                        p.Book
                    );
                if (p.StartChapter != p.EndChapter)
                    err += string.Format(
                        "||Chapter|{0}|{1}|{2}|",
                        p.Section?.Sequencenum,
                        p.Sequencenum,
                        p.Reference
                    );
                if (p.StartVerse == 0)
                    err += string.Format(
                        "||Reference|{0}|{1}|{2}|",
                        p.Section?.Sequencenum,
                        p.Sequencenum,
                        p.Reference
                    );
            }
            ;
            return err.Length > 0 
                ? "ReferenceError:" + err 
                : err;
        }

        public string GetTranscription(List<Mediafile> mediafiles)
        {
            string transcription = "";
            mediafiles.ForEach(m => transcription += m.Transcription + " ");
            //remove timestamps
            string pattern = @"\([0-9]{1,2}:[0-9]{2}(:[0-9]{2})?\)";
            return Regex.Replace(transcription, pattern, "");
        }

        public async Task<List<ParatextChapter>> SyncPlanAsync(
            UserSecret userSecret,
            int planId,
            int artifactTypeId
        )
        {
            User? currentUser = CurrentUserRepository.GetCurrentUser();
            Plan? plan = dbContext.Plans.Find(planId);
            Artifacttype? type = dbContext.Artifacttypes.Find(artifactTypeId);
            string paratextId = ParatextHelpers.ParatextProject(
                plan?.ProjectId,
                type?.Typename ?? "",
                ProjectIntegrationRepository
            );
            IEnumerable<Mediafile> mediafiles = MediafileService.ReadyToSync(
                planId,
                artifactTypeId
            );
            List<Passage> passages = new();
            foreach (Mediafile m in mediafiles)
            {
                if (passages.FindIndex(p => p.Id == m.PassageId) < 0 && m.Passage != null)
                    passages.Add(m.Passage);
            }
            ;

            if (artifactTypeId == 0)
            {
                IEnumerable<Passage> backwardCompatibilityPassages = PassageService.ReadyToSync(
                    planId
                );
                foreach (Passage bc in backwardCompatibilityPassages)
                {
                    if (passages.FindIndex(p => p.Id == bc.Id) < 0)
                        passages.Add(bc);
                }
                ;
            }
            string err = VerifyReferences(userSecret, passages, paratextId);
            if (err.Length > 0)
                throw new Exception(err);

            //assume startChapter=endChapter for all passages
            IEnumerable<BookChapter> book_chapters = BookChapters(passages);

            bool addNumbers = true; //this would be an option in the plan? or the project?

            List<ParatextChapter> chapterList = await GetPassageChaptersAsync(
                userSecret,
                paratextId,
                book_chapters
            );
            chapterList.ForEach(c => c.NewUSX = new XElement(c.OriginalUSX));
            ParatextChapter chapter;
            using (IDbContextTransaction transaction = dbContext.Database.BeginTransaction())
            {
                foreach (BookChapter bookchapter in book_chapters)
                {
                    chapter = chapterList
                        .Where(c => c.Book == bookchapter.Book && c.Chapter == bookchapter.Chapter)
                        .First();
                    //log it
                    Paratextsync history =
                        new(
                            currentUser?.Id ?? 0,
                            planId,
                            paratextId,
                            bookchapter.ToString(),
                            chapter.OriginalUSX?.ToString() ?? ""
                        );
                    _ = logDbContext.Add(history);
                    _ = logDbContext.SaveChanges();
                    //make sure we have the chapter number
                    try
                    {
                        chapter.NewUSX = ParatextHelpers.AddParatextChapter(
                            chapter.NewUSX,
                            chapter.Book,
                            chapter.Chapter
                        );
                        if (HttpContext != null)
                            HttpContext.SetFP("paratext");
                        foreach (
                            Passage passage in passages.Where(
                                p => p.Book == chapter.Book && p.StartChapter == chapter.Chapter
                            )
                        )
                        {
                            try
                            {
                                List<Mediafile> psgMedia = mediafiles
                                    .Where(m => m.PassageId == passage.Id)
                                    .OrderBy(m => m.DateCreated)
                                    .ToList();
                                string transcription = "";
                                if (psgMedia.Count > 0)
                                    transcription = GetTranscription(psgMedia);

                                chapter.NewUSX = ParatextHelpers.GenerateParatextData(
                                    chapter.NewUSX,
                                    passage,
                                    transcription,
                                    addNumbers
                                );
                                //log it
                                _ = logDbContext.Paratextsyncpassages.Add(
                                    new Paratextsyncpassage(
                                        currentUser?.Id ?? 0,
                                        history?.Id ?? 0,
                                        passage?.Reference ?? "",
                                        transcription,
                                        chapter.NewUSX?.ToString() ?? ""
                                    )
                                );
                                _ = logDbContext.SaveChanges();
                                for (int ix = 0; ix < psgMedia.Count; ix++)
                                {
                                    Mediafile mediafile = psgMedia[ix];
                                    mediafile.Transcriptionstate = "done";
                                    //Debug.WriteLine("mediafile {0}", mediafile.Id);
                                    _ = dbContext.Mediafiles.Update(mediafile);
                                }
                            }
                            catch (Exception ex)
                            {
                                //log it
                                Logger.LogError(
                                    "Paratext Error passage {message} {id} {reference}",
                                    ex.Message,
                                    passage.Id,
                                    passage.Reference
                                );
                                if (history != null)
                                {
                                    _ = logDbContext.Paratextsyncpassages.Add(
                                        new Paratextsyncpassage(
                                            currentUser?.Id ?? 0,
                                            history.Id,
                                            passage?.Reference ?? "",
                                            ex.Message + ex.InnerException?.Message ?? ""
                                        )
                                    );
                                    _ = logDbContext.SaveChanges();
                                }
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(
                            "Paratext Error generating Chapter text {message} {book} {chapter}: {orig} {history}",
                            ex.Message,
                            chapter.Book,
                            chapter.Chapter,
                            chapter.OriginalUSX?.ToString() ?? "",
                            history
                        );
                        if (history != null)
                        {
                            history.Err =
                                ex.Message
                                + "::"
                                + (ex.InnerException != null ? ex.InnerException.Message : "");
                            //log it
                            _ = logDbContext.Paratextsyncs.Update(history);
                            _ = logDbContext.SaveChanges();
                        }
                        Logger.LogError(
                            "Paratext Error generating Chapter text {message} {book} {chapter}: {orig}",
                            ex.Message,
                            chapter.Book,
                            chapter.Chapter,
                            chapter.OriginalUSX?.ToString() ?? ""
                        );
                        transaction.Rollback();
                        throw;
                    }
                }
                foreach (ParatextChapter c in chapterList)
                {
                    try
                    {
                        string bookText = await UpdateChapterTextAsync(
                            userSecret,
                            paratextId,
                            c.Book,
                            c.Chapter,
                            c.Revision ?? "",
                            c.NewUSX?.ToString() ?? ""
                        );
                        XElement bookTextElem = XElement.Parse(bookText);
                        c.NewValue = bookTextElem.Value;
                        c.NewUSX = bookTextElem.Element("usx");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        //log it
                        Paratextsync? history =
                            new(
                                currentUser?.Id ?? 0,
                                planId,
                                paratextId,
                                c.Book + c.Chapter,
                                c.NewUSX?.ToString() ?? "",
                                ex.Message
                            );
                        _ = logDbContext.Paratextsyncs.Add(history);
                        _ = logDbContext.SaveChanges();

                        Logger.LogError(
                            "Paratext Error updating Chapter text {M0} {B1} {C2}: {O3} {N4}",
                            ex.Message,
                            c.Book,
                            c.Chapter,
                            c.OriginalUSX?.ToString(),
                            c.NewUSX?.ToString() ?? ""
                        );
                        throw;
                    }
                }
                _ = dbContext.SaveChanges();
                transaction.Commit();
            }
            return chapterList;
        }

        public async Task<List<ParatextChapter>> SyncProjectAsync(
            UserSecret userSecret,
            int projectId,
            int artifactTypeId
        )
        {
            Project? project = await ProjectService.GetWithPlansAsync(projectId);
            List<ParatextChapter> chapters = new();
            if (project != null)
                foreach (Plan p in project.Plans)
                {
                    if (!p.Archived)
                        chapters.AddRange(await SyncPlanAsync(userSecret, p.Id, artifactTypeId));
                }
            return chapters;
        }
    }
}
