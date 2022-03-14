using IdentityModel;
using JsonApiDotNetCore.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Linq;
using SIL.Logging.Models;
using SIL.Logging.Repositories;
using SIL.ObjectModel;
using SIL.Paratext.Models;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services
{
    /// <summary>Exchanges data with PT projects in the cloud.</summary>
    public class ParatextService :  DisposableBase, IParatextService
    {
        //private readonly IOptions<ParatextOptions> _options;
        protected ICurrentUserContext CurrentUserContext;
        protected readonly AppDbContext dbContext;
        private readonly HttpClientHandler _httpClientHandler;
        private readonly HttpClient _dataAccessClient;
        private readonly HttpClient _registryClient;

        private MediafileService MediafileService;
        private PassageService PassageService;
        private PassageStateChangeService PassageStateChangeService;
        private SectionService SectionService;
        private ProjectService ProjectService;
        private PlanService PlanService;
        private InvitationService InvitationService;
        private ParatextTokenService ParatextTokenService;
        private readonly ParatextTokenRepository _userSecretRepository;
        public CurrentUserRepository CurrentUserRepository { get; }

        private HttpContext HttpContext;
        protected ILogger<ParatextService> Logger { get; set; }
        ParatextSyncRepository ParatextSyncRepository;
        ParatextSyncPassageRepository ParatextSyncPassageRepository;
        ParatextTokenHistoryRepository TokenHistoryRepo;


        public ParatextService(AppDbContextResolver contextResolver,
            IHostingEnvironment env,
            IHttpContextAccessor httpContextAccessor,
            ICurrentUserContext currentUserContext,
            PassageService passageService,
            MediafileService mediafileService,
            PassageStateChangeService passageStateChangeService,
            SectionService sectionService,
            PlanService planService,
            ProjectService projectService,
            ParatextTokenService ptService,
            ParatextTokenRepository userSecrets,
            ParatextSyncRepository paratextSyncRepository,
            ParatextSyncPassageRepository paratextSyncPassageRepository,
            CurrentUserRepository currentUserRepository,
            ParatextTokenHistoryRepository tokenHistoryRepo,
            InvitationService invitationService,
           ILoggerFactory loggerFactory)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            HttpContext = httpContextAccessor.HttpContext;
            _userSecretRepository = userSecrets;
            PassageService = passageService;
            MediafileService = mediafileService;
            PassageStateChangeService = passageStateChangeService;
            SectionService = sectionService;
            ProjectService = projectService;
            PlanService = planService;
            ParatextTokenService = ptService;
            CurrentUserContext = currentUserContext;
            CurrentUserRepository = currentUserRepository;
            ParatextSyncRepository = paratextSyncRepository;
            ParatextSyncPassageRepository =paratextSyncPassageRepository;
            TokenHistoryRepo = tokenHistoryRepo;
            InvitationService = invitationService;
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
            User currentUser = CurrentUserRepository.GetCurrentUser();
            if (currentUser == null) throw new Exception("Unable to get current user information");
            UserSecret newPTToken = CurrentUserContext.ParatextLogin(GetVarOrDefault("SIL_TR_PARATEXT_AUTH0_CONNECTION", "Paratext-Transcriber"), currentUser.Id);

            if (newPTToken == null)
            {
                throw new Exception("User is not logged in to Paratext-Transcriber");
            }
            //get existing
            IEnumerable<ParatextToken> tokens = ParatextTokenService.GetAsync().Result;
            if (tokens != null && tokens.Count() > 0)
            {
                ParatextToken token = tokens.First();
                if (newPTToken.ParatextTokens.IssuedAt > token.IssuedAt && newPTToken.ParatextTokens.RefreshToken != null)
                {
                    token.AccessToken = newPTToken.ParatextTokens.AccessToken;
                    token.RefreshToken = newPTToken.ParatextTokens.RefreshToken;
                    _userSecretRepository.UpdateAsync(token.Id, token);
                    //TokenHistoryRepo.CreateAsync(new ParatextTokenHistory(currentUser.Id, token.AccessToken, token.RefreshToken, "From Login"));
                }
                else
                {
                    newPTToken.ParatextTokens = token;
                    //TokenHistoryRepo.CreateAsync(new ParatextTokenHistory(currentUser.Id, token.AccessToken, token.RefreshToken, "From DB"));
                }
            }
            else
            {
                newPTToken.ParatextTokens = _userSecretRepository.CreateAsync(newPTToken.ParatextTokens).Result;
                //TokenHistoryRepo.CreateAsync(new ParatextTokenHistory(currentUser.Id, newPTToken.ParatextTokens.AccessToken, newPTToken.ParatextTokens.RefreshToken, "From First Login"));
            }
            return newPTToken;
        }
        private Claim GetClaim(string AccessToken, string claimtype)
        {
            User currentUser = CurrentUserRepository.GetCurrentUser();
            JwtSecurityToken accessToken = new JwtSecurityToken(AccessToken);
            Claim claim = accessToken.Claims.FirstOrDefault(c => c.Type == claimtype);
            System.Console.WriteLine("XXX CLAIM GetClaim {0}", claim != null ? claim.ToString(): "null");
            return claim;
        }
        public async Task<IReadOnlyList<ParatextProject>> GetProjectsAsync(UserSecret userSecret)
        {
            VerifyUserSecret(userSecret);

            Claim usernameClaim = GetClaim(userSecret.ParatextTokens.AccessToken, "username");
            string username = usernameClaim?.Value;
            string response = await CallApiAsync(_dataAccessClient, userSecret, HttpMethod.Get, "projects");
            XElement reposElem = XElement.Parse(response);
            List<ParatextProject> projects = new List<ParatextProject>();
            foreach (XElement repoElem in reposElem.Elements("repo"))
            {
                string projId = (string)repoElem.Element("projid");
                XElement userElem = repoElem.Element("users")?.Elements("user")
                    ?.FirstOrDefault(ue => (string)ue.Element("name") == username);
                string role = (string)userElem?.Element("role");
                IEnumerable<string> projectids = ProjectService.LinkedToParatext(projId).Select(p => p.Id.ToString());

                projects.Add(new ParatextProject
                {
                    ParatextId = projId,
                    ProjectType = (string)repoElem.Element("projecttype"),
                    BaseProject = (string)repoElem.Element("baseprojid"),
                    ShortName = (string)repoElem.Element("proj"),
                    Name = "",
                    LanguageTag = "",
                    LanguageName = "",
                    ProjectIds = projectids,
                    IsConnectable = (role == ParatextProjectRoles.Administrator || role == ParatextProjectRoles.Translator),
                    CurrentUserRole = role,
                });
            }
            
            //get more info for those projects that are registered
            response = await CallApiAsync(_registryClient, userSecret, HttpMethod.Get, "projects");
            JArray projectArray = JArray.Parse(response);

            foreach (JToken projectObj in projectArray)
            {
                JToken identificationObj = projectObj["identification_systemId"]
                    .FirstOrDefault(id => (string)id["type"] == "paratext");
                if (identificationObj == null)
                    continue;
                string paratextId = (string)identificationObj["text"];
                ParatextProject proj = projects.FirstOrDefault(p => p.ParatextId == paratextId);
                if (proj == null)
                    continue;

                string name = (string)identificationObj["fullname"];
                string langName = (string)projectObj["language_iso"];
                string langTag = (string)projectObj["language_ldml"];
                //if (StandardSubtags.TryGetLanguageFromIso3Code(langName, out LanguageSubtag subtag))
                //   langName = subtag.Name;
                proj.Name = name;
                proj.LanguageName = langName;
                proj.LanguageTag = langTag;
            }

            //now go through them again to link BT to base project
            foreach (ParatextProject proj in projects)
            {
                List<ParatextProject> subProjects = projects.FindAll(p => p.BaseProject == proj.ParatextId);
                subProjects.ForEach(sp =>
                {
                    if (sp.Name.Length == 0) sp.Name = sp.ProjectType + " " + proj.Name;
                    sp.LanguageName += (sp.LanguageName.Length > 0 ? "," : "") + proj.LanguageName;
                    sp.LanguageTag += (sp.LanguageTag.Length > 0 ? "," : "") + proj.LanguageTag;
                });
            }
            
            return projects;
        }
        public async Task<IReadOnlyList<ParatextProject>> GetProjectsAsync(UserSecret userSecret, string languageTag)
        {
            IReadOnlyList<ParatextProject> projects = await GetProjectsAsync(userSecret);
            return projects.Where(p => p.LanguageTag.Split(",").Contains(languageTag)).ToList();
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
        public async Task<Attempt<string>> TryGetUserEmailsAsync(UserSecret userSecret, string inviteId)
        {
            try
            {
                VerifyUserSecret(userSecret);
                if (!int.TryParse(inviteId, out int id))
                    return Attempt.Failure("Invalid invitation Id");

                Invitation invite = InvitationService.Get(id);
                string response = await CallApiAsync(_registryClient, userSecret, HttpMethod.Get,
                    $"users/?emails.address=" +invite.Email);
                JArray memberObj = JArray.Parse(response);
                if (memberObj.Count > 0 && memberObj[0]["username"].ToString() == GetParatextUsername(userSecret))
                {
                    invite.Email = CurrentUserContext.Email;
                    invite.Accepted = true;
                    await InvitationService.UpdateAsync(id, invite);
                    return Attempt.Success(inviteId);
                }
                else
                    return Attempt.Failure("notfound");
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
            System.Console.WriteLine("XXX GetParatextUsername");

            VerifyUserSecret(userSecret);
            try
            {
                Claim usernameClaim = GetClaim(userSecret.ParatextTokens.AccessToken, "username");
                System.Console.WriteLine("XXX GetClaim service {0}", usernameClaim != null ? usernameClaim.ToString() : "null");
                return usernameClaim?.Value;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("XXX CLAIM service exception:", ex.GetType(), ex.Message);
                throw (ex);
            }
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
            Console.WriteLine($"text/{projectId}/{bookId}/{chapter}");
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

        private async Task<UserSecret> RefreshAccessTokenAsync(UserSecret userSecret)
        {
            VerifyUserSecret(userSecret);
            if (userSecret.ParatextTokens.RefreshToken == null)
            {
                throw new Exception("401 RefreshTokenNull");
            }
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api8/token");
            JObject requestObj = new JObject(
                new JProperty("grant_type", "refresh_token"),
                new JProperty("client_id", GetVarOrDefault("SIL_TR_PARATEXT_CLIENT_ID", "")),
                new JProperty("client_secret", GetVarOrDefault("SIL_TR_PARATEXT_CLIENT_SECRET", "")),
                new JProperty("refresh_token", userSecret.ParatextTokens.RefreshToken));
            request.Content = new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _registryClient.SendAsync(request);
            string responseJson = await response.Content.ReadAsStringAsync();
            JObject responseObj = JObject.Parse(responseJson);

            //log it
            //requestObj["client_secret"] = "XXX";
            //await TokenHistoryRepo.CreateAsync(new ParatextTokenHistory(userSecret.ParatextTokens.UserId, (string)responseObj["access_token"], (string)responseObj["refresh_token"], requestObj.ToString(), response.ReasonPhrase + responseObj));
            
            response.EnsureSuccessStatusCode();
            
            userSecret.ParatextTokens.AccessToken = (string)responseObj["access_token"];
            userSecret.ParatextTokens.RefreshToken = (string)responseObj["refresh_token"];
            if (userSecret.ParatextTokens.RefreshToken != null)
                await _userSecretRepository.UpdateAsync(userSecret.ParatextTokens.Id, userSecret.ParatextTokens);
            else throw new Exception("401 RefreshTokenNull.  Expected on Dev and QA.  Login again with Paratext connection.");

            //log it
            //await TokenHistoryRepo.CreateAsync(new ParatextTokenHistory(userSecret.ParatextTokens.UserId, userSecret.ParatextTokens.AccessToken, userSecret.ParatextTokens.RefreshToken, "AfterRefresh"));

            return userSecret;
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
                    userSecret = await RefreshAccessTokenAsync(userSecret);
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
            public override string ToString()
            {
                return Book + ' ' + Chapter;
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
        private IEnumerable<BookChapter> BookChapters(IEnumerable<Passage> passages)
        {
            return passages.Select(p => new BookChapter(p.Book, p.StartChapter)).Distinct();
        }
        public async Task<List<ParatextChapter>> GetSectionChaptersAsync(UserSecret userSecret, int sectionId, int typeId)
        {
            ArtifactType type = dbContext.Artifacttypes.Find(typeId);
            string paratextId = ParatextHelpers.ParatextProject(SectionService.GetProjectId(sectionId), type != null ? type.Typename : "", ProjectService);
            IQueryable<Passage> passages = PassageService.GetBySection(sectionId);
            return await GetPassageChaptersAsync(userSecret, paratextId, BookChapters(passages));
        }
        public int PlanPassagesToSyncCount(int planId, int artifactTypeId)
        {
            return MediafileService.ReadyToSync(planId, artifactTypeId).Count();
        }

        public async Task<string> PassageTextAsync(int passageId, int typeId)
        {
            IEnumerable<Passage> passages = PassageService.Get(passageId).ToList();
            Passage passage = passages.FirstOrDefault();
            if (passage == null)
            {
                throw new Exception("Passage not found or user does not have access to passage.");
            }
            ArtifactType type = dbContext.Artifacttypes.Find(typeId);

            string paratextId = ParatextHelpers.ParatextProject(PassageService.GetProjectId(passage), type != null ? type.Typename : "", ProjectService);
            UserSecret userSecret = ParatextLogin();
            string err = VerifyReferences(userSecret, passages, paratextId);
            if (err.Length > 0) throw new Exception(err);

            //assume startChapter=endChapter for all passages
            IEnumerable<BookChapter> book_chapters = BookChapters(passages);
            List<ParatextChapter> chapterList = await GetPassageChaptersAsync(userSecret, paratextId, book_chapters);
            return ParatextHelpers.GetParatextData(chapterList.First().OriginalUSX, passage);
        }
        public async Task<int> ProjectPassagesToSyncCountAsync(int projectId, int artifactTypeid)
        {
            Project project = await ProjectService.GetWithPlansAsync(projectId);
            int total = 0;
            foreach (Plan p in project.Plans)
            {
                 
                total += MediafileService.ReadyToSync(p.Id, artifactTypeid).Count();
            }
            return total;
        }
        public string VerifyReferences(UserSecret userSecret, IEnumerable<Passage> passages, string paratextId)
        {
            IReadOnlyList<string> books = GetBooksAsync(userSecret, paratextId).Result;
            string err = "";
            passages.ForEach(p =>
            {
                if (p.Book == "")
                    err += string.Format("||Empty Book|{0}|{1}", p.Section.Sequencenum, p.Sequencenum);
                else if (!books.Contains(p.Book))
                    err += string.Format("||Missing Book|{0}|{1}|{2}", p.Section.Sequencenum, p.Sequencenum, p.Book);
                if (p.StartChapter != p.EndChapter)
                    err += string.Format("||Chapter|{0}|{1}|{2}", p.Section.Sequencenum, p.Sequencenum, p.Reference);
                if (p.StartVerse == 0)
                    err += string.Format("||Reference|{0}|{1}|{2}", p.Section.Sequencenum, p.Sequencenum, p.Reference);
            });
            if (err.Length>0) return "ReferenceError:" + err;
            return err;
        }
        public string GetTranscription(List<Mediafile> mediafiles)
        {
            string transcription = "";
            mediafiles.ForEach(m => transcription += m.Transcription + " ");
            //remove timestamps
            string pattern = @"\([0-9]{1,2}:[0-9]{2}(:[0-9]{2})?\)";
            return Regex.Replace(transcription, pattern, "");
        }

        public async Task<List<ParatextChapter>> SyncPlanAsync(UserSecret userSecret, int planId, int artifactTypeId)
        {
            User currentUser = CurrentUserRepository.GetCurrentUser();
            Plan plan = PlanService.Get(planId);
            ArtifactType type = dbContext.Artifacttypes.Find(artifactTypeId);
            string paratextId = ParatextHelpers.ParatextProject(plan.ProjectId, type != null ? type.Typename : "", ProjectService);
            IQueryable<Mediafile> mediafiles = MediafileService.ReadyToSync(planId, artifactTypeId);
            List<Passage> passages = new List<Passage>();
            mediafiles.ForEach(m => {
                if (passages.FindIndex(p => p.Id == m.PassageId) < 0)
                    passages.Add(m.Passage);
            });

            if (artifactTypeId == 0)
            {
                IQueryable<Passage> backwardCompatibilityPassages = PassageService.ReadyToSync(planId);
                backwardCompatibilityPassages.ForEach(bc => {
                    if (passages.FindIndex(p => p.Id == bc.Id) < 0)
                        passages.Add(bc);
                });
            }
            string err = VerifyReferences(userSecret, passages, paratextId);
            if (err.Length > 0) throw new Exception(err);

            //assume startChapter=endChapter for all passages
            IEnumerable<BookChapter> book_chapters = BookChapters(passages);

            bool addNumbers = true; //this would be an option in the plan? or the project? 

            List<ParatextChapter> chapterList = await GetPassageChaptersAsync(userSecret, paratextId, book_chapters);
            chapterList.ForEach(c => c.NewUSX = new XElement(c.OriginalUSX));
            ParatextChapter chapter;
            using (IDbContextTransaction transaction = dbContext.Database.BeginTransaction())  
            {

                foreach (BookChapter bookchapter in book_chapters)
                {
                    chapter = chapterList.Where(c => c.Book == bookchapter.Book &&  c.Chapter == bookchapter.Chapter).First();
                    //log it
                    ParatextSync history = await ParatextSyncRepository.CreateAsync(new ParatextSync(currentUser.Id, planId, paratextId, bookchapter.ToString(), chapter.OriginalUSX.ToString()));
                    //make sure we have the chapter number
                    try
                    {
                        chapter.NewUSX = ParatextHelpers.AddParatextChapter(chapter.NewUSX, chapter.Book, chapter.Chapter);
                        HttpContext.SetFP("paratext");
                        foreach (Passage passage in passages.Where(p => p.Book == chapter.Book && p.StartChapter == chapter.Chapter))
                        {
                            try
                            {
                                List<Mediafile> psgMedia = mediafiles.Where(m => m.PassageId == passage.Id).OrderBy(m => m.DateCreated).ToList();
                                string transcription = "";
                                if (psgMedia.Count > 0)
                                    transcription = GetTranscription(psgMedia);
                                else transcription = PassageService.GetTranscription(passage) ?? "";

                                chapter.NewUSX = ParatextHelpers.GenerateParatextData(chapter.NewUSX, passage, transcription, addNumbers);
                                //log it
                                await ParatextSyncPassageRepository.CreateAsync(new ParatextSyncPassage(currentUser.Id, history.Id, passage.Reference, transcription, chapter.NewUSX.ToString()));
                                for (int ix=0; ix < psgMedia.Count; ix++)
                                {
                                    Mediafile mediafile = psgMedia[ix];
                                    mediafile.Transcriptionstate = "done";
                                    //Debug.WriteLine("mediafile {0}", mediafile.Id);
                                    await MediafileService.UpdateAsync(mediafile.Id, mediafile);
                                }
                            }
                            catch (Exception ex)
                            {
                                //log it
                                await ParatextSyncPassageRepository.CreateAsync(new ParatextSyncPassage(currentUser.Id, history.Id, passage.Reference, ex.Message+ex.InnerException.Message));
                                transaction.Rollback();
                                throw ex;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        history.Err = ex.Message + ex.InnerException.Message;
                        //log it
                        await ParatextSyncRepository.UpdateAsync(history.Id, history);
                        Logger.LogError("Paratext Error generating Chapter text {0} {1} {2}: {3}", ex.Message, chapter.Book, chapter.Chapter,chapter.OriginalUSX.ToString());
                        transaction.Rollback();
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
                        //log it
                        ParatextSync history = await ParatextSyncRepository.CreateAsync(new ParatextSync(currentUser.Id, planId, paratextId, c.Book+c.Chapter, c.NewUSX.ToString(), ex.Message));
                        Logger.LogError("Paratext Error updating Chapter text {0} {1} {2}: {3} {4}", ex.Message, c.Book, c.Chapter, c.OriginalUSX.ToString(), c.NewUSX.ToString());
                        throw ex;
                    }
                }
                transaction.Commit();
            }
            return chapterList;
        }
        public async Task<List<ParatextChapter>> SyncProjectAsync(UserSecret userSecret, int projectId, int artifactTypeId)
        {
            var project = await ProjectService.GetWithPlansAsync(projectId);
            List<ParatextChapter> chapters = new List<ParatextChapter>();
            foreach (Plan p in project.Plans)
            {
                if (!p.Archived)
                    chapters.AddRange(await SyncPlanAsync(userSecret, p.Id, artifactTypeId));
            }
            return chapters;

        }

        protected override void DisposeManagedResources()
        {
            _dataAccessClient.Dispose();
            _registryClient.Dispose();
            _httpClientHandler.Dispose();
        }
    }
}