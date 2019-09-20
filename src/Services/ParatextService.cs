using IdentityModel;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
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

namespace SIL.Transcriber.Services
{
    /// <summary>Exchanges data with PT projects in the cloud.</summary>
    public class ParatextService : IParatextService //  DisposableBase, IParatextService
    {
        //private readonly IOptions<ParatextOptions> _options;
        //private readonly IRepository<UserSecret> _userSecret; //IRepository
                                                              //private readonly IRealtimeService _realtimeService;
        private readonly HttpClientHandler _httpClientHandler;
        private readonly HttpClient _dataAccessClient;
        private readonly HttpClient _registryClient;

        private PassageService PassageService;
        private SectionService SectionService;
        private ProjectService ProjectService;
        private PlanService PlanService;

        public ParatextService(IHostingEnvironment env,
            PassageService passageService,
            SectionService sectionService,
            PlanService planService,
            ProjectService projectService) //,IOptions<ParatextOptions> options,
            // IRepository<UserSecret> userSecret) , IRealtimeService realtimeService)
        {
            // _options = options;
            //_userSecret = userSecret;
            //_realtimeService = realtimeService;
            PassageService = passageService;
            SectionService = sectionService;
            ProjectService = projectService;
            PlanService = planService;

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
        public async Task<UserSecret> ParatextLogin()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "api8/token");
            var requestObj = new JObject(
                new JProperty("grant_type", "password"),
                new JProperty("username", "Sara Hentzel"),
                new JProperty("password", "XGAJ4F-V2RWXF-C23QFG-7UZ168-B7UR6V"),
                //new JProperty("authorization_code", "3D9gskbYkLtUnL5aNq412cNi62xayGu4ZhVprUBIztpeP"),
                new JProperty("scope", "users.org:read orgs:read projects:read data_access projects.members:read projects.members:write")); 

            request.Content = new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json");
            request.Headers.Add("Authorization", "Bearer eyJhbGciOiJFUzI1NiJ9.eyJzY29wZXMiOlsib2F1dGg6YXV0aG9yaXphdGlvbl9jb2RlIiwib2F1dGg6cmVmcmVzaF90b2tlbiIsIm9hdXRoOnBhc3N3b3JkIl0sImp0aSI6IkpvemMzWWZQam42UEd0S0gzIiwiYXVkIjpbImh0dHBzOi8vcmVnaXN0cnktZGV2LnBhcmF0ZXh0Lm9yZyJdLCJwcmltYXJ5X29yZ19pZCI6ImM4YmQyNDIxNmRiOWU2YjJiYWVmZDE5MiIsInN1YiI6IllEckpnamY4TWhkNUg1eHNhIiwiYXpwIjoiWURySmdqZjhNaGQ1SDV4c2EiLCJpYXQiOjE1NjY5MzM1MzQsImlzcyI6InB0cmVnIn0.uxFYKfZBu8eUbZav4JotABoqnXA9z6JiEDOohM8d0MJHCxNRtRDOlGovmtKqsVc22-jcSc4ZoXv0G_ce0RXYxg");
            HttpResponseMessage response = await _registryClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Unable to login to Paratext");
            }
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            var responseObj = JObject.Parse(responseJson);
            var userSecret = new UserSecret
            {
                ParatextTokens = new Tokens
                {
                    AccessToken = (string)responseObj["access_token"],
                    RefreshToken = (string)responseObj["refresh_token"]
                }
            };
            return userSecret;
        }

        public async Task<System.Collections.Generic.IReadOnlyList<ParatextProject>> GetProjectsAsync(UserSecret userSecret)
        {
            var accessToken = new JwtSecurityToken(userSecret.ParatextTokens.AccessToken);
            Claim usernameClaim = accessToken.Claims.FirstOrDefault(c => c.Type == "username");
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
            //
            //Dictionary<string, SFProject> existingProjects = (await _realtimeService
            //    .QuerySnapshots<SFProject>(RootDataTypes.Projects)
            //    .Where(p => repos.Keys.Contains(p.ParatextId))
            //    .ToListAsync()).ToDictionary(p => p.ParatextId);

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
                /*

                    // determine if the project is connectable, i.e. either the project exists and the user hasn't been
                    // added to the project, or the project doesn't exist and the user is the administrator
                    bool isConnectable;
                    bool isConnected = false;
                    string projectId = null;
                    if (existingProjects.TryGetValue(paratextId, out SFProject project))
                    {
                        projectId = project.Id;
                        isConnected = true;
                        isConnectable = !project.UserRoles.ContainsKey(userSecret.Id);
                    }
                    else if (role == SFProjectRoles.Administrator)
                    {
                        isConnectable = true;
                    }
                    else
                    {
                        isConnectable = false;
                    }
*/
                    var langName = (string)projectObj["language_iso"];
                    //if (StandardSubtags.TryGetLanguageFromIso3Code(langName, out LanguageSubtag subtag))
                    //    langName = subtag.Name;

                    projects.Add(new ParatextProject
                    {
                        ParatextId = paratextId,
                        Name = (string)identificationObj["fullname"],
                        LanguageTag = (string)projectObj["language_ldml"],
                        LanguageName = langName,
                        ProjectId = "",// projectId,
                        IsConnectable = false, // isConnectable,
                        IsConnected = false // isConnected
                    });
                
            }

            return projects;
        }

        public async Task<Attempt<string>> TryGetProjectRoleAsync(UserSecret userSecret, string paratextId)
        {
            if (userSecret.ParatextTokens == null)
                return Attempt.Failure((string)null, "The current user is not signed into Paratext.");
            try
            {
                var accessToken = new JwtSecurityToken(userSecret.ParatextTokens.AccessToken);
                Claim subClaim = accessToken.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Subject);
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

        public string GetParatextUsername(UserSecret userSecret)
        {
            if (userSecret.ParatextTokens == null)
                return null;
            var accessToken = new JwtSecurityToken(userSecret.ParatextTokens.AccessToken);
            Claim usernameClaim = accessToken.Claims.FirstOrDefault(c => c.Type == "username");
            return usernameClaim?.Value;
        }

        public async Task<System.Collections.Generic.IReadOnlyList<string>> GetBooksAsync(UserSecret userSecret, string projectId)
        {
            string response = await CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, $"books/{projectId}");
            var books = XElement.Parse(response);
            string[] bookIds = books.Elements("Book").Select(b => (string)b.Attribute("id")).ToArray();
            return bookIds;
        }

        public Task<string> GetBookTextAsync(UserSecret userSecret, string projectId, string bookId)
        {
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, $"text/{projectId}/{bookId}");
        }
        public Task<string> GetChapterTextAsync(UserSecret userSecret, string projectId, string bookId, int chapter)
        {
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, $"text/{projectId}/{bookId}/{chapter}");
        }
        /// <summary>Update cloud with new edits in usxText and return the combined result.</summary>
        public Task<string> UpdateChapterTextAsync(UserSecret userSecret, string projectId, string bookId, int chapter,
            string revision, string usxText)
        {
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Post,
                $"text/{projectId}/{revision}/{bookId}/{chapter}", usxText);
        }
        /// <summary>Update cloud with new edits in usxText and return the combined result.</summary>
        public Task<string> UpdateBookTextAsync(UserSecret userSecret, string projectId, string bookId,
            string revision, string usxText)
        {
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Post,
                $"text/{projectId}/{revision}/{bookId}", usxText);
        }

        public Task<string> GetNotesAsync(UserSecret userSecret, string projectId, string bookId)
        {
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, $"notes/{projectId}/{bookId}");
        }

        public Task<string> UpdateNotesAsync(UserSecret userSecret, string projectId, string notesText)
        {
            return CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Post, $"notes/{projectId}", notesText);
        }

        private async Task RefreshAccessTokenAsync(UserSecret userSecret)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "api8/token");

            //ParatextOptions options = _options.Value;
            var requestObj = new JObject(
                new JProperty("grant_type", "refresh_token"),
                new JProperty("client_id", "TODO"), //where to get a clientid? options.ClientId),
                new JProperty("client_secret", "TODO"), // options.ClientSecret),
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
            if (userSecret == null)
                throw new SecurityException("The current user is not signed into Paratext.");

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
        private async Task<List<ParatextChapter>> GetPassageChaptersAsync(UserSecret userSecret, string paratextId, IQueryable<Passage> passages)
        {
            var chapterList = new List<ParatextChapter>();
            string book_chapter = "";
            foreach (Passage p in passages)
            {
                if (p.Book + p.StartChapter != book_chapter)
                {
                    book_chapter = p.Book + p.StartChapter;
                    chapterList.Add(await GetParatextChapterAsync(userSecret, paratextId, p.Book, p.StartChapter));
                }
                if (p.Book + p.EndChapter != book_chapter)
                {
                    book_chapter = p.Book + p.EndChapter;
                    chapterList.Add(GetParatextChapterAsync(userSecret, paratextId, p.Book, p.EndChapter).Result);
                }
            }
            return chapterList;
        }

        public async Task<List<ParatextChapter>> GetSectionChaptersAsync(UserSecret userSecret, int sectionId)
        {
            string paratextId = ParatextHelpers.ParatextProject(SectionService.GetProjectId(sectionId), ProjectService);
            var passages = PassageService.GetBySection(sectionId).OrderBy(p => p.Book);
            return await GetPassageChaptersAsync(userSecret, paratextId, passages);
        }
        public async Task<List<ParatextChapter>> SyncPlanAsync(UserSecret userSecret, int planId)
        {
            var plan = PlanService.GetWithSections(planId);
            string paratextId = ParatextHelpers.ParatextProject(plan.ProjectId, ProjectService);
            //gather all the passages from all the sections that are approved
            SortedList<string, SortedList<int, Passage>> passages = new SortedList<string, SortedList<int, Passage>>();
            foreach (Section s in plan.Sections)
            {
                foreach (PassageSection ps in s.PassageSections)
                {
                    if (ps.Passage.ReadyToSync)
                    {
                        if (!passages.ContainsKey(ps.Passage.Book + ps.Passage.StartChapter.ToString().PadLeft(3)))
                            passages.Add(ps.Passage.Book + ps.Passage.StartChapter.ToString().PadLeft(3), new SortedList<int, Passage>());
                        passages[ps.Passage.Book + ps.Passage.StartChapter.ToString().PadLeft(3)].Add(ps.Passage.StartVerse, ps.Passage);
                    }
                }
            }
            var chapterList = await GetPassageChaptersAsync(userSecret, paratextId, passages.Keys);
            chapterList.ForEach(c => c.NewUSX = c.OriginalUSX);
            ParatextChapter chapter;
            
            foreach (string bookchapter in passages.Keys)
            {
                chapter = chapterList.Where(c => c.Book == bookchapter.Substring(0, bookchapter.Length-3) &&  c.Chapter == int.Parse(bookchapter.Substring(bookchapter.Length - 3))).First();
                foreach (Passage p in passages[bookchapter].Values)
                    chapter.NewUSX = ParatextHelpers.GenerateParatextData(p, chapter.NewUSX, PassageService.GetTranscription(p) ?? "");
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
        public async Task<List<ParatextChapter>> SyncSectionAsync(UserSecret userSecret, int sectionId)
        {
            string paratextId = ParatextHelpers.ParatextProject(SectionService.GetProjectId(sectionId), ProjectService);
            var passages = PassageService.GetBySection(sectionId).OrderBy(p => p.Book);
            var chapterList = await GetPassageChaptersAsync(userSecret, paratextId, passages);
            chapterList.ForEach(c => c.NewUSX = c.OriginalUSX);
            ParatextChapter chapter;
            foreach (Passage p in passages)
            {
                chapter = chapterList.Where(c => c.Chapter == p.StartChapter).First();
                chapter.NewUSX = ParatextHelpers.GenerateParatextData(p, chapter.NewUSX, PassageService.GetTranscription(p) ?? "");
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

        protected /* override  DisposableBase */ void DisposeManagedResources()
        {
            _dataAccessClient.Dispose();
            _registryClient.Dispose();
            _httpClientHandler.Dispose();
        }
    }
}

