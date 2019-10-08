using IdentityModel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using SIL.Paratext.Models;
using SIL.Transcriber.Models;
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
        //private readonly IRealtimeService _realtimeService;
        private readonly HttpClientHandler _httpClientHandler;
        private readonly HttpClient _dataAccessClient;
        private readonly HttpClient _registryClient;

        private PassageService PassageService;
        private SectionService SectionService;
        private ProjectService ProjectService;
        private PlanService PlanService;
        private HttpContext HttpContext;


        public ParatextService(IHostingEnvironment env,
            IHttpContextAccessor httpContextAccessor,
            ICurrentUserContext currentUserContext,
            PassageService passageService,
            SectionService sectionService,
            PlanService planService,
            ProjectService projectService) //,IOptions<ParatextOptions> options,
                                           //IRepository<UserSecret> userSecret)// , IRealtimeService realtimeService)
        {
            HttpContext = httpContextAccessor.HttpContext;
            // _options = options;
            // _userSecret = userSecret;
            //_realtimeService = realtimeService;
            PassageService = passageService;
            SectionService = sectionService;
            ProjectService = projectService;
            PlanService = planService;
            CurrentUserContext = currentUserContext;

            _httpClientHandler = new HttpClientHandler();
            _dataAccessClient = new HttpClient(_httpClientHandler);
            _registryClient = new HttpClient(_httpClientHandler);
            if (env.IsDevelopment() || env.IsEnvironment("Testing"))
            {
                _httpClientHandler.ServerCertificateCustomValidationCallback
                    = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                _dataAccessClient.BaseAddress = new Uri("https://data-access-dev.paratext.org/");
                _registryClient.BaseAddress = new Uri("https://registry-dev.paratext.org/");
            }
            else
            {
                _dataAccessClient.BaseAddress = new Uri("https://data-access.paratext.org/");
                _registryClient.BaseAddress = new Uri("https://registry.paratext.org/");
            }
            _registryClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        public UserSecret ParatextLogin()
        {
            var userSecret = CurrentUserContext.ParatextLogin("Paratext-Transcriber");

            if (userSecret == null)
            {
                throw new Exception("User is not logged in to Paratext-Transcriber");
            }
            return userSecret;
        }
        private Claim GetClaim(string AccessToken, string claimtype)
        {
            var accessToken = new JwtSecurityToken(AccessToken);
            return accessToken.Claims.FirstOrDefault(c => c.Type == claimtype);
        }
        public async Task<System.Collections.Generic.IReadOnlyList<ParatextProject>> GetProjectsAsync(UserSecret userSecret)
        {
            VerifyUserSecret(userSecret);

            Claim usernameClaim = GetClaim(userSecret.ParatextTokens.AccessToken, "username");
            string username = usernameClaim?.Value;
            string response = await CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, "projects");
            var reposElem = XElement.Parse(response);
            var repos = new Dictionary<string, string>();
            foreach (XElement repoElem in reposElem.Elements("repo"))
            {
                var projId = (string)repoElem.Element("projid");
                XElement userElem = repoElem.Element("users")?.Elements("user")
                    ?.FirstOrDefault(ue => (string)ue.Element("name") == username);
                repos[projId] = (string)userElem?.Element("role");
            }
            response = await CallApiAsync(_registryClient, userSecret, HttpMethod.Get, "projects");
            var projectArray = JArray.Parse(response);
            var projects = new List<ParatextProject>();
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
                int? projectId = null;
                Project project = ProjectService.LinkedToParatext(paratextId).Result;
                if (project != null)
                {
                    projectId = project.Id;
                    isConnected = true;
                    isConnectable = true; // !project.UserRoles.ContainsKey(userSecret.Id);
                }
                else if (role == ParatextProjectRoles.Administrator || role == ParatextProjectRoles.Translator)
                {
                    isConnectable = true;
                }
 
                var langName = (string)projectObj["language_iso"];
                //if (StandardSubtags.TryGetLanguageFromIso3Code(langName, out LanguageSubtag subtag))
                //    langName = subtag.Name;

                projects.Add(new ParatextProject
                {
                    ParatextId = paratextId,
                    Name = (string)identificationObj["fullname"],
                    LanguageTag = (string)projectObj["language_ldml"],
                    LanguageName = langName,
                    ProjectId = projectId,
                    IsConnected = isConnected,
                    IsConnectable = isConnectable,
                    CurrentUserRole = role,
                });
            }

            return projects;
        }

        public async Task<Attempt<string>> TryGetProjectRoleAsync(UserSecret userSecret, string paratextId)
        {
            VerifyUserSecret(userSecret);
            try
            {
                Claim subClaim = GetClaim(userSecret.ParatextTokens.AccessToken, JwtClaimTypes.Subject);
                string response = await CallApiAsync(_registryClient, userSecret, HttpMethod.Get,
                    $"projects/{paratextId}/members/{subClaim.Value}");
                var memberObj = JObject.Parse(response);
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
            return true;
        }
        public string GetParatextUsername(UserSecret userSecret)
        {
            VerifyUserSecret(userSecret);
            Claim usernameClaim = GetClaim(userSecret.ParatextTokens.AccessToken, "username");
            return usernameClaim?.Value;
        }

        public async Task<System.Collections.Generic.IReadOnlyList<string>> GetBooksAsync(UserSecret userSecret, string projectId)
        {
            VerifyUserSecret(userSecret);
            string response = await CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, $"books/{projectId}");
            var books = XElement.Parse(response);
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
            VerifyUserSecret(userSecret);
            var request = new HttpRequestMessage(HttpMethod.Post, "api8/token");
            var requestObj = new JObject(
                new JProperty("grant_type", "refresh_token"),
                new JProperty("client_id", GetVarOrDefault("SIL_TR_PARATEXT_CLIENT_ID", "")),
                new JProperty("client_secret", GetVarOrDefault("SIL_TR_PARATEXT_CLIENT_SECRET", "")),
                new JProperty("refresh_token", userSecret.ParatextTokens.RefreshToken));
            request.Content = new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _registryClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            var responseObj = JObject.Parse(responseJson);
            userSecret.ParatextTokens = new Tokens
            {
                AccessToken = (string)responseObj["access_token"],
                RefreshToken = (string)responseObj["refresh_token"],
            };
            //TODO await _userSecret.UpdateAsync(userSecret, b => b.Set(u => u.ParatextTokens, userSecret.ParatextTokens));
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

                var request = new HttpRequestMessage(method, $"api8/{url}");
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
            var bookTextElem = XElement.Parse(bookText);
            chapter.Project = (string)bookTextElem.Attribute("project");
            chapter.Revision = (string)bookTextElem.Attribute("revision");
            chapter.OriginalValue = bookTextElem.Value;
            chapter.OriginalUSX = bookTextElem.Element("usx");
            return chapter;
        }
        private async Task<List<ParatextChapter>> GetPassageChaptersAsync(UserSecret userSecret, string paratextId, IList<string> chapters)
        {
            var chapterList = new List<ParatextChapter>();

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
            var chapterList = new List<ParatextChapter>();

            foreach (BookChapter bc in book_chapters)
            {
                chapterList.Add(await GetParatextChapterAsync(userSecret, paratextId, bc.Book, bc.Chapter));
            }
            return chapterList;
        }
        private IEnumerable<BookChapter> BookChapters(IQueryable<Passage> passages)
        {
            return passages.Select(p => new BookChapter(p.Book, p.StartChapter)).Distinct<BookChapter>();
        }
        public async Task<List<ParatextChapter>> GetSectionChaptersAsync(UserSecret userSecret, int sectionId)
        {
            string paratextId = ParatextHelpers.ParatextProject(SectionService.GetProjectId(sectionId), ProjectService);
            var passages = PassageService.GetBySection(sectionId);
            return await GetPassageChaptersAsync(userSecret, paratextId, BookChapters(passages));
        }
        public async Task<int> PlanPassagesToSyncCountAsync(int planId)
        {
            var passages = await PassageService.ReadyToSyncAsync(planId);
            return passages.Count();
        }
        public async Task<int> ProjectPassagesToSyncCountAsync(int projectId)
        {
            var project = await ProjectService.GetWithPlansAsync(projectId);
            int total = 0;
            foreach(Plan p in project.Plans)
            {
                var passages = await PassageService.ReadyToSyncAsync(p.Id);
                total += passages.Count();
            }
            return total;
        }

        public async Task<List<ParatextChapter>> SyncPlanAsync(UserSecret userSecret, int planId)
        {
            var plan = PlanService.Get(planId);
            var passages = await PassageService.ReadyToSyncAsync(planId);
            //assume startChapter=endChapter for all passages
            IEnumerable<BookChapter> book_chapters = BookChapters(passages);

            string paratextId = ParatextHelpers.ParatextProject(plan.ProjectId, ProjectService);
            var chapterList = await GetPassageChaptersAsync(userSecret, paratextId, book_chapters);
            chapterList.ForEach(c => c.NewUSX = c.OriginalUSX);
            ParatextChapter chapter;

            foreach (BookChapter bookchapter in book_chapters)
            {
                chapter = chapterList.Where(c => c.Book == bookchapter.Book &&  c.Chapter == bookchapter.Chapter).First();
                //make sure we have the chapter number
                chapter.NewUSX = ParatextHelpers.AddParatextChapter(chapter.NewUSX, chapter.Book, chapter.Chapter);
                IEnumerable<SectionSummary> ss = SectionService.GetSectionSummary(planId, chapter.Book, chapter.Chapter);
                chapter.NewUSX = ParatextHelpers.AddSectionHeaders(chapter.NewUSX, ss);
                foreach (Passage passage in passages.Where(p=>p.Book == chapter.Book && p.StartChapter == chapter.Chapter))
                    chapter.NewUSX = ParatextHelpers.GenerateParatextData(chapter.NewUSX, passage, PassageService.GetTranscription(passage) ?? "", ss);
            }
            foreach (ParatextChapter c in chapterList)
            {
                string bookText = await UpdateChapterTextAsync(userSecret, paratextId, c.Book, c.Chapter, c.Revision, c.NewUSX.ToString());
                var bookTextElem = XElement.Parse(bookText);
                c.NewValue = bookTextElem.Value;
                c.NewUSX = bookTextElem.Element("usx");
            }
            return chapterList;
        }
        public async Task<List<ParatextChapter>> SyncProjectAsync(UserSecret userSecret, int projectId)
        {
            var project = await ProjectService.GetWithPlansAsync(projectId);
            List<ParatextChapter> chapters = new List<ParatextChapter>();
            foreach (Plan p in project.Plans)
            {
                chapters.AddRange(await SyncPlanAsync(userSecret, p.Id));
            }
            return chapters;

        }

        protected /* override  DisposableBase */
            void DisposeManagedResources()
        {
            _dataAccessClient.Dispose();
            _registryClient.Dispose();
            _httpClientHandler.Dispose();
        }
    }
}

