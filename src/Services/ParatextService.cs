using IdentityModel;
using JsonApiDotNetCore.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SIL.Paratext.Models;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services
{
    /// <summary>Exchanges data with PT projects in the cloud.</summary>
    public class ParatextService : IParatextService //  DisposableBase, IParatextService
    {
        //private readonly IOptions<ParatextOptions> _options;
        protected ICurrentUserContext CurrentUserContext;
        protected readonly AppDbContext dbContext;
        private readonly HttpClientHandler _httpClientHandler;
        private readonly HttpClient _dataAccessClient;
        private readonly HttpClient _registryClient;

        private PassageService PassageService;
        private PassageStateChangeService PassageStateChangeService;
        private SectionService SectionService;
        private ProjectService ProjectService;
        private PlanService PlanService;
        private ParatextTokenService ParatextTokenService;
        private readonly IEntityRepository<ParatextToken> _userSecretRepository;
        public CurrentUserRepository CurrentUserRepository { get; }

        private HttpContext HttpContext;
        protected ILogger<ParatextService> Logger { get; set; }


        public ParatextService(IDbContextResolver contextResolver, 
            IHostingEnvironment env,
            IHttpContextAccessor httpContextAccessor,
            ICurrentUserContext currentUserContext,
            PassageService passageService,
            PassageStateChangeService passageStateChangeService,
            SectionService sectionService,
            PlanService planService,
            ProjectService projectService,
            ParatextTokenService ptService,
            IEntityRepository<ParatextToken> userSecrets,
             CurrentUserRepository currentUserRepository,
           ILoggerFactory loggerFactory)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            HttpContext = httpContextAccessor.HttpContext;
            _userSecretRepository = userSecrets;
            PassageService = passageService;
            PassageStateChangeService = passageStateChangeService;
            SectionService = sectionService;
            ProjectService = projectService;
            PlanService = planService;
            ParatextTokenService = ptService;
            CurrentUserContext = currentUserContext;
            CurrentUserRepository = currentUserRepository;
            this.Logger = loggerFactory.CreateLogger<ParatextService>();

            _httpClientHandler = new HttpClientHandler();
            _dataAccessClient = new HttpClient(_httpClientHandler);
            _registryClient = new HttpClient(_httpClientHandler);
            if (env.IsDevelopment() || env.IsStaging() || env.IsEnvironment("Testing"))
            {
                _httpClientHandler.ServerCertificateCustomValidationCallback
                    = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            _dataAccessClient.BaseAddress = new Uri(GetVarOrDefault("SIL_TR_PARATEXT_DATA", "https://data-access.paratext.org/"));
            _registryClient.BaseAddress = new Uri(GetVarOrDefault("SIL_TR_PARATEXT_REGISTRY", "https://registry.paratext.org/"));
            _registryClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        public UserSecret ParatextLogin()
        {
            User currentUser = CurrentUserRepository.GetCurrentUser().Result;
            UserSecret newPTToken = CurrentUserContext.ParatextLogin(GetVarOrDefault("SIL_TR_PARATEXT_AUTH0_CONNECTION", "Paratext-Transcriber"), currentUser.Id);

            if (newPTToken == null)
            {
                throw new Exception("User is not logged in to Paratext-Transcriber");
            }
            //get existing
            IEnumerable<ParatextToken> tokens = ParatextTokenService.GetAsync().Result;
            Console.WriteLine("console.writeline stored paratext token count " + tokens.Count().ToString());
            Logger.LogInformation("logger.loginformation stored paratext token count " + tokens.Count().ToString());
            if (tokens != null && tokens.Count() > 0)
            {
                ParatextToken token = tokens.First();
                Logger.LogInformation("Logged in ParatextRefreshToken {0} {1}", newPTToken.ParatextTokens.IssuedAt, newPTToken.ParatextTokens.RefreshToken);
                Logger.LogInformation("Stored ParatextRefreshToken {0} {1}", token.IssuedAt, token.RefreshToken);
                if (newPTToken.ParatextTokens.IssuedAt > token.IssuedAt && newPTToken.ParatextTokens.RefreshToken != null)
                {
                    token.AccessToken = newPTToken.ParatextTokens.AccessToken;
                    token.RefreshToken = newPTToken.ParatextTokens.RefreshToken;
                    Console.WriteLine("Update to token" + token.ToString());
                    _userSecretRepository.UpdateAsync(token.Id, token);
                }
                else
                {
                    newPTToken.ParatextTokens = token;
                }
            }
            else
            {
                newPTToken.ParatextTokens = _userSecretRepository.CreateAsync(newPTToken.ParatextTokens).Result;
            }
            return newPTToken;
        }
        private Claim GetClaim(string AccessToken, string claimtype)
        {
            JwtSecurityToken accessToken = new JwtSecurityToken(AccessToken);
            return accessToken.Claims.FirstOrDefault(c => c.Type == claimtype);
        }
        public async Task<IReadOnlyList<ParatextProject>> GetProjectsAsync(UserSecret userSecret)
        {
            VerifyUserSecret(userSecret);

            Claim usernameClaim = GetClaim(userSecret.ParatextTokens.AccessToken, "username");
            string username = usernameClaim?.Value;
            string response = await CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, "projects");
            XElement reposElem = XElement.Parse(response);
            Dictionary<string, string> repos = new Dictionary<string, string>();
            foreach (XElement repoElem in reposElem.Elements("repo"))
            {
                string projId = (string)repoElem.Element("projid");
                XElement userElem = repoElem.Element("users")?.Elements("user")
                    ?.FirstOrDefault(ue => (string)ue.Element("name") == username);
                repos[projId] = (string)userElem?.Element("role");
            }
            response = await CallApiAsync(_registryClient, userSecret, HttpMethod.Get, "projects");
            JArray projectArray = JArray.Parse(response);
            List<ParatextProject> projects = new List<ParatextProject>();
            foreach (JToken projectObj in projectArray)
            {
                JToken identificationObj = projectObj["identification_systemId"]
                    .FirstOrDefault(id => (string)id["type"] == "paratext");
                if (identificationObj == null)
                    continue;
                string paratextId = (string)identificationObj["text"];
                if (!repos.TryGetValue(paratextId, out string role))
                    continue;

                // determine if the project is connectable, i.e. either the project exists and the user hasn't been
                // added to the project, or the project doesn't exist and the user is the administrator
                bool isConnected = false;
                bool isConnectable = false;
                IEnumerable<int> projectids = ProjectService.LinkedToParatext(paratextId).Select(p => p.Id);
                if (projects != null && projects.Count() > 0)
                {
                    isConnected = true;
                    isConnectable = true;// !project.UserRoles.ContainsKey(userSecret.Id);
                }
                else if (role == ParatextProjectRoles.Administrator || role == ParatextProjectRoles.Translator)
                {
                    isConnectable = true;
                }

                string langName = (string)projectObj["language_iso"];
                //if (StandardSubtags.TryGetLanguageFromIso3Code(langName, out LanguageSubtag subtag))
                //    langName = subtag.Name;

                projects.Add(new ParatextProject
                {
                    ParatextId = paratextId,
                    Name = (string)identificationObj["fullname"],
                    LanguageTag = (string)projectObj["language_ldml"],
                    LanguageName = langName,
                    ProjectIds = projectids,
                    IsConnected = isConnected,
                    IsConnectable = isConnectable,
                    CurrentUserRole = role,
                });
            }

            return projects;
        }
        public async Task<IReadOnlyList<ParatextProject>> GetProjectsAsync(UserSecret userSecret, string languageTag)
        {
            IReadOnlyList<ParatextProject> projects = await GetProjectsAsync(userSecret);
            return projects.Where(p => p.LanguageTag == languageTag).ToList();
        }

        public async Task<Attempt<string>> TryGetProjectRoleAsync(UserSecret userSecret, string paratextId)
        {
            VerifyUserSecret(userSecret);
            try
            {
                Claim subClaim = GetClaim(userSecret.ParatextTokens.AccessToken, JwtClaimTypes.Subject);
                string response = await CallApiAsync(_registryClient, userSecret, HttpMethod.Get,
                    $"projects/{paratextId}/members/{subClaim.Value}");
                JObject memberObj = JObject.Parse(response);
                return Attempt.Success((string)memberObj["role"]);
            }
            catch (HttpRequestException ex)
            {
                return Attempt.Failure((string)null, ex.Message);
            }
        }
        private bool VerifyUserSecret(UserSecret userSecret)
        {
            if (userSecret is null || userSecret.ParatextTokens is null)
                throw new SecurityException("Paratext credentials not provided.");
            if (userSecret.ParatextTokens.AccessToken is null || userSecret.ParatextTokens.AccessToken.Length == 0)
                throw new SecurityException("Current user is not logged in to Paratext.");
            Logger.LogInformation("Current ParatextToken: Issued:{0} Now: {1} ValidTo: {2} refreshToken {3}",
                userSecret.ParatextTokens.IssuedAt,
                DateTime.UtcNow, 
                userSecret.ParatextTokens.ValidTo, 
                userSecret.ParatextTokens.RefreshToken);
            return true;
        }
        public string GetParatextUsername(UserSecret userSecret)
        {
            VerifyUserSecret(userSecret);
            Claim usernameClaim = GetClaim(userSecret.ParatextTokens.AccessToken, "username");
            return usernameClaim?.Value;
        }

        public async Task<IReadOnlyList<string>> GetBooksAsync(UserSecret userSecret, string projectId)
        {
            VerifyUserSecret(userSecret);
            string response = await CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, $"books/{projectId}");
            XElement books = XElement.Parse(response);
            string[] bookIds = books.Elements("Book").Select(b => (string)b.Attribute("id")).ToArray();
            return bookIds;
        }

        public Task<string> GetBookTextAsync(UserSecret userSecret, string projectId, string bookId)
        {
            VerifyUserSecret(userSecret);
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, $"text/{projectId}/{bookId}");
        }
        public Task<string> GetChapterTextAsync(UserSecret userSecret, string projectId, string bookId, int chapter)
        {
            VerifyUserSecret(userSecret);
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, $"text/{projectId}/{bookId}/{chapter}");
        }
        /// <summary>Update cloud with new edits in usxText and return the combined result.</summary>
        public Task<string> UpdateChapterTextAsync(UserSecret userSecret, string projectId, string bookId, int chapter,
            string revision, string usxText)
        {
            VerifyUserSecret(userSecret);
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Post,
                $"text/{projectId}/{revision}/{bookId}/{chapter}", usxText);
        }
        /// <summary>Update cloud with new edits in usxText and return the combined result.</summary>
        public Task<string> UpdateBookTextAsync(UserSecret userSecret, string projectId, string bookId,
            string revision, string usxText)
        {
            VerifyUserSecret(userSecret);
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Post,
                $"text/{projectId}/{revision}/{bookId}", usxText);
        }

        public Task<string> GetNotesAsync(UserSecret userSecret, string projectId, string bookId)
        {
            VerifyUserSecret(userSecret);
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, $"notes/{projectId}/{bookId}");
        }

        public Task<string> UpdateNotesAsync(UserSecret userSecret, string projectId, string notesText)
        {
            VerifyUserSecret(userSecret);
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Post, $"notes/{projectId}", notesText);
        }

        private async Task RefreshAccessTokenAsync(UserSecret userSecret)
        {
            Logger.LogInformation("Refresh ParatextRefreshToken {0}", userSecret.ParatextTokens.RefreshToken);
            VerifyUserSecret(userSecret);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api8/token");
            JObject requestObj = new JObject(
                new JProperty("grant_type", "refresh_token"),
                new JProperty("client_id", GetVarOrDefault("SIL_TR_PARATEXT_CLIENT_ID", "")),
                new JProperty("client_secret", GetVarOrDefault("SIL_TR_PARATEXT_CLIENT_SECRET", "")),
                new JProperty("refresh_token", userSecret.ParatextTokens.RefreshToken));
            request.Content = new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _registryClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log(LogLevel.Error, "Paratext Refresh with latest refresh token " + response.IsSuccessStatusCode.ToString() + response.ReasonPhrase);

                request = new HttpRequestMessage(HttpMethod.Post, "api8/token");
                requestObj = new JObject(
                    new JProperty("grant_type", "refresh_token"),
                    new JProperty("client_id", GetVarOrDefault("SIL_TR_PARATEXT_CLIENT_ID", "")),
                    new JProperty("client_secret", GetVarOrDefault("SIL_TR_PARATEXT_CLIENT_SECRET", ""))
                   );
                request.Content = new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json");
                response = await _registryClient.SendAsync(request);
            }
            Logger.Log(response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Error, "Paratext Refresh" + response.IsSuccessStatusCode.ToString() + response.ReasonPhrase);
            response.EnsureSuccessStatusCode();
            string responseJson = await response.Content.ReadAsStringAsync();
            JObject responseObj = JObject.Parse(responseJson);
            userSecret.ParatextTokens.AccessToken = (string)responseObj["access_token"];
            userSecret.ParatextTokens.RefreshToken = (string)responseObj["refresh_token"];
            Logger.LogInformation("new ParatextRefreshToken {0}", userSecret.ParatextTokens.RefreshToken);
            await _userSecretRepository.UpdateAsync(userSecret.ParatextTokens.Id, userSecret.ParatextTokens);
        }

        private async Task<string> CallApiAsync(HttpClient client, UserSecret userSecret, HttpMethod method,
            string url, string content = null)
        {
            VerifyUserSecret(userSecret);

            bool expired = !userSecret.ParatextTokens.ValidateLifetime();
            bool refreshed = false;
            while (!refreshed)
            {
                if (expired)
                {
                    await RefreshAccessTokenAsync(userSecret);
                    refreshed = true;
                }

                HttpRequestMessage request = new HttpRequestMessage(method, $"api8/{url}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
                    userSecret.ParatextTokens.AccessToken);
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
                        $"HTTP Request error, Code: {response.StatusCode}, Content: {error}");
                }
            }

            throw new SecurityException("The current user's Paratext access token is invalid.");
        }
        private async Task<ParatextChapter> GetParatextChapterAsync(UserSecret userSecret, string paratextId, string book, int number)
        {
            VerifyUserSecret(userSecret);

            ParatextChapter chapter = new ParatextChapter();
            chapter.Book = book;
            chapter.Chapter = number;
            //get the text out of paratext
            string bookText = await GetChapterTextAsync(userSecret, paratextId, book, number);
            XElement bookTextElem = XElement.Parse(bookText);
            chapter.Project = (string)bookTextElem.Attribute("project");
            chapter.Revision = (string)bookTextElem.Attribute("revision");
            chapter.OriginalValue = bookTextElem.Value;
            chapter.OriginalUSX = bookTextElem.Element("usx");
            return chapter;
        }
        private async Task<List<ParatextChapter>> GetPassageChaptersAsync(UserSecret userSecret, string paratextId, IList<string> chapters)
        {
            List<ParatextChapter> chapterList = new List<ParatextChapter>();

            foreach (string c in chapters)
            {
                chapterList.Add(await GetParatextChapterAsync(userSecret, paratextId, c.Substring(0, c.Length - 3), int.Parse(c.Substring(c.Length - 3))));
            }
            return chapterList;
        }
        private class BookChapter : IEquatable<BookChapter>
        {
            public string Book { get; }
            public int Chapter { get; }
            public BookChapter(string book, int chapter)
            {
                Book = book;
                Chapter = chapter;
            }
            public bool Equals(BookChapter other)
            {

                //Check whether the compared object is null. 
                if (Object.ReferenceEquals(other, null)) return false;

                //Check whether the compared object references the same data. 
                if (Object.ReferenceEquals(this, other)) return true;

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
        }
        private async Task<List<ParatextChapter>> GetPassageChaptersAsync(UserSecret userSecret, string paratextId, IEnumerable<BookChapter> book_chapters)
        {
            List<ParatextChapter> chapterList = new List<ParatextChapter>();

            foreach (BookChapter bc in book_chapters)
            {
                chapterList.Add(await GetParatextChapterAsync(userSecret, paratextId, bc.Book, bc.Chapter));
            }
            return chapterList;
        }
        private IEnumerable<BookChapter> BookChapters(IQueryable<Passage> passages)
        {
            return passages.Select(p => new BookChapter(p.Book, p.StartChapter)).Distinct();
        }
        public async Task<List<ParatextChapter>> GetSectionChaptersAsync(UserSecret userSecret, int sectionId)
        {
            string paratextId = ParatextHelpers.ParatextProject(SectionService.GetProjectId(sectionId), ProjectService);
            IQueryable<Passage> passages = PassageService.GetBySection(sectionId);
            return await GetPassageChaptersAsync(userSecret, paratextId, BookChapters(passages));
        }
        public int PlanPassagesToSyncCount(int planId)
        {
            IQueryable<Passage> passages = PassageService.ReadyToSync(planId);
            return passages.Count();
        }
        public async Task<int> ProjectPassagesToSyncCountAsync(int projectId)
        {
            Project project = await ProjectService.GetWithPlansAsync(projectId);
            int total = 0;
            foreach (Plan p in project.Plans)
            {
                IQueryable<Passage> passages = PassageService.ReadyToSync(p.Id);
                total += passages.Count();
            }
            return total;
        }

        public async Task<List<ParatextChapter>> SyncPlanAsync(UserSecret userSecret, int planId)
        {
            Plan plan = PlanService.Get(planId);
            IQueryable<Passage> passages = PassageService.ReadyToSync(planId);
            //assume startChapter=endChapter for all passages
            IEnumerable<BookChapter> book_chapters = BookChapters(passages);

            bool addNumbers = true; //this would be an option in the plan? or the project? 

            string paratextId = ParatextHelpers.ParatextProject(plan.ProjectId, ProjectService);
            List<ParatextChapter> chapterList = await GetPassageChaptersAsync(userSecret, paratextId, book_chapters);
            chapterList.ForEach(c => c.NewUSX = c.OriginalUSX);
            ParatextChapter chapter;
            using (IDbContextTransaction transaction = dbContext.Database.BeginTransaction())  
            {

                foreach (BookChapter bookchapter in book_chapters)
                {
                    Logger.LogInformation("{0} {1}", bookchapter.Book, bookchapter.Chapter);
                    chapter = chapterList.Where(c => c.Book == bookchapter.Book &&  c.Chapter == bookchapter.Chapter).First();
                    //make sure we have the chapter number
                    Logger.LogInformation("Add Chapter");
                    try
                    {
                        chapter.NewUSX = ParatextHelpers.AddParatextChapter(chapter.NewUSX, chapter.Book, chapter.Chapter);
                        IEnumerable<SectionSummary> ss = SectionService.GetSectionSummary(planId, chapter.Book, chapter.Chapter);
                        HttpContext.SetFP("paratext");
                        foreach (Passage passage in passages.Where(p => p.Book == chapter.Book && p.StartChapter == chapter.Chapter))
                        {
                            chapter.NewUSX = ParatextHelpers.GenerateParatextData(chapter.NewUSX, passage, PassageService.GetTranscription(passage) ?? "", ss, addNumbers);
                            passage.State = "done";
                            await PassageService.UpdateAsync(passage.Id, passage);
                            await PassageStateChangeService.CreateAsync(passage, "Paratext");
                        }
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Logger.LogError("Paratext Error generating Chapter text {0} {1} {2}: {3}", ex.Message, chapter.Book, chapter.Chapter,chapter.OriginalUSX.ToString());
                        throw ex;
                    }
                }
                foreach (ParatextChapter c in chapterList)
                {
                    try
                    {
                        string bookText = await UpdateChapterTextAsync(userSecret, paratextId, c.Book, c.Chapter, c.Revision, c.NewUSX.ToString());
                        XElement bookTextElem = XElement.Parse(bookText);
                        c.NewValue = bookTextElem.Value;
                        c.NewUSX = bookTextElem.Element("usx");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Logger.LogError("Paratext Error updating Chapter text {0} {1} {2}: {3} {4}", ex.Message, c.Book, c.Chapter, c.OriginalUSX.ToString(), c.NewUSX.ToString());
                        throw ex;
                    }
                }
                transaction.Commit();
            }
            return chapterList;
        }
        public async Task<List<ParatextChapter>> SyncProjectAsync(UserSecret userSecret, int projectId)
        {
            var project = await ProjectService.GetWithPlansAsync(projectId);
            List<ParatextChapter> chapters = new List<ParatextChapter>();
            foreach (Plan p in project.Plans)
            {
                if (!p.Archived)
                    chapters.AddRange(await SyncPlanAsync(userSecret, p.Id));
            }
            return chapters;

        }
    }
}