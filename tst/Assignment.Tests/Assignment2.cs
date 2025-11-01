using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Realworlddotnet.Api.Features.Articles;
using Realworlddotnet.Api.Features.Users;
using Realworlddotnet.Core.Dto;
using Realworlddotnet.Core.Entities;
using Realworlddotnet.Data.Contexts;

namespace Assignment.Tests;

public class Assignment2 : IAsyncLifetime
{
    // async initialised
    private WebApplication server = null!;
    private HttpClient client = null!;

    [Fact(DisplayName = "Assignment 2.0 - Sanity Check if database is up - should pass"),
     Trait("Number", "2.0")]
    public async Task SanityCheckShouldPass()
    {
        await Login();

        var response = await client.GetAsync($"api/articles");
        response.EnsureSuccessStatusCode();
        var articleResponse = await response.Content.ReadFromJsonAsync<ArticlesResponse>();

        articleResponse.Should().NotBeNull();
        articleResponse!.ArticlesCount.Should().Be(5000);
    }

    [Fact(DisplayName = "Assignment 2.1 - Database Check New Table SearchCount"),
     Trait("Number", "2.1")]
    public async Task DatabaseSearchCountTable()
    {
        await using var context = await CreateContext();
        var checkSearchCountTable =
            context.Articles.FromSql($"SELECT name FROM sqlite_master WHERE type='table' AND name='SearchCount'");
        checkSearchCountTable.Count().Should().BePositive("a table named SearchCount should be present");
    }

    [Fact(DisplayName = "Assignment 2.2 - Database Check New Column 'ReadCount' for Table 'Articles'"),
     Trait("Number", "2.2")]
    public async Task DatabaseCheckReadCountInArticle()
    {
        await using var context = await CreateContext();
        var checkArticleSearchTable =
            context.Articles.FromSql($"SELECT ReadCount FROM Articles");
        checkArticleSearchTable.Count().Should().BePositive("A column 'ReadCount' should be present in table 'Articles'");
    }

    [Fact(DisplayName = "Assignment 2.3 - Database Check New Column 'Timestamp' for Table 'ArticleFavorites'"),
     Trait("Number", "2.3")]
    public async Task DatabaseCheckTimestampInArticleFavorites()
    {
        await using var context = await CreateContext();
        var checkArticleSearchTable =
            context.Articles.FromSql($"SELECT Timestamp FROM ArticleFavorites");
        checkArticleSearchTable.Count().Should().BePositive("A column 'Timestamp' should be present in table 'ArticleFavorites'");
    }

    [Fact(DisplayName = "Assignment 2.4 - Check REST-API Search"),
     Trait("Number", "2.4")]
    public async Task CheckSearchViaRESTAPI()
    {
        await Login();

        // search 'objekt'
        int expectedCount = 1166;
        var response = await client.GetAsync(
            $"api/search?query=objekt&limit=2&offset=5");
        response.EnsureSuccessStatusCode();
        var articleResponse = await response.Content.ReadFromJsonAsync<ArticlesResponse>();
        articleResponse!.ArticlesCount.Should().Be(expectedCount);
        
        // search 'objekt+datenbank+code'
        expectedCount = 1140;
        response = await client.GetAsync(
            $"api/search?query=objekt+datenbank+code&limit=2&offset=5");
        response.EnsureSuccessStatusCode();
        articleResponse = await response.Content.ReadFromJsonAsync<ArticlesResponse>();
        articleResponse!.ArticlesCount.Should().Be(expectedCount);
        
        // search 'compiler+sprache+java'
        expectedCount = 36;
        response = await client.GetAsync(
            $"api/search?query=compiler+sprache+java");
        response.EnsureSuccessStatusCode();
        articleResponse = await response.Content.ReadFromJsonAsync<ArticlesResponse>();
        articleResponse!.ArticlesCount.Should().Be(expectedCount);
    }

    [Fact(DisplayName = "Assignment 2.5 - Check if article reads increases count"),
     Trait("Number", "2.5")]
    public async Task CheckReadArticlesIncreasesReadCount()
    {
        IQueryable<Article> article2Test;
        string slug;
        int prevCount;
        int nextCount;

        using (var context = await CreateContext())
        {
            article2Test = context.Articles.FromSql($"SELECT * FROM Articles WHERE Id='31046FEA-C853-44B1-AC42-9A0FD2D2ADB9'");
            slug = article2Test.FirstAsync().Result.Slug;
            prevCount = article2Test.FirstAsync().Result.ReadCount;
        }

        // call article
        var response = await client.GetAsync($"api/articles/{slug}");
        response.EnsureSuccessStatusCode();

        using (var context = await CreateContext())
        {
            article2Test = context.Articles.FromSql($"SELECT * FROM Articles WHERE Id='31046FEA-C853-44B1-AC42-9A0FD2D2ADB9'");
            nextCount = article2Test.FirstAsync().Result.ReadCount;
        }

        nextCount.Should().Be(prevCount + 1);
    }

    [Fact(DisplayName = "Assignment 2.6 - Check if like updates timestamp"),
     Trait("Number", "2.6")]
    public async Task CheckLikeUpdatesTimestamp()
    {
        var articleID = "4775F8D5-DB66-459A-B5DE-4A755C334F2D";
        
        await Login();

        string slug;
        bool flagAlreadyLiked;
        
        using (var context = await CreateContext())
        {
            // get articles slug
            var article2Test =
                context.Articles.FromSql($"SELECT * FROM Articles WHERE Id={articleID}");
            slug = article2Test.FirstAsync().Result.Slug;
            
            var articlefav2Test =
                context.ArticleFavorites.FromSql($"SELECT * FROM ArticleFavorites WHERE ArticleId={articleID} AND Username='sadduck169'");
            
            var articleFavorite = articlefav2Test.FirstOrDefaultAsync().Result;
            flagAlreadyLiked = articleFavorite != null;
        }
        
        HttpResponseMessage response;
        
        if (flagAlreadyLiked)
        {
            // unlike article is already liked
            response = await client.DeleteAsync(
                $"api/articles/{slug}/favorite");
            response.EnsureSuccessStatusCode();
        }

        // measure start time
        var startTime = DateTime.UtcNow;
        
        // like article
        response = await client.PostAsync(
            $"api/articles/{slug}/favorite",null);
        response.EnsureSuccessStatusCode();
        
        // measure end time
        var endTime = DateTime.UtcNow;

        
        using (var newContext = await CreateContext())
        {
            // get article like timestamp
            var articlefav2Test =
                newContext.ArticleFavorites.FromSql($"SELECT * FROM ArticleFavorites WHERE ArticleId={articleID} AND Username='sadduck169'");
            var favTime = articlefav2Test.FirstAsync().Result.Timestamp;
            
            favTime.Should().BeAfter(startTime);
            favTime.Should().BeBefore(endTime);
        }
        
        if (!flagAlreadyLiked)
        {
            // reset favorite if article was not liked 
            response = await client.DeleteAsync(
                $"api/articles/{slug}/favorite");
            response.EnsureSuccessStatusCode();
        }
    }
    
    private async Task<ConduitContext> CreateContext()
    {
        var connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = "../../../../../realworld.db",
                Mode = SqliteOpenMode.ReadOnly // tests should be deterministic & reproducible
            }.ToString();
        var contextOptions = new DbContextOptionsBuilder<ConduitContext>()
            .LogTo(Console.WriteLine).EnableSensitiveDataLogging().EnableDetailedErrors()
            .UseSqlite(connectionString)
            .Options;

        var context = new ConduitContext(contextOptions);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    public async Task Login()
    {
        var user = new LoginUserDto("jonas.lambert@example.com", "ou8123");
        var userEnvelope = new UserEnvelope<LoginUserDto>(user);

        HttpResponseMessage response = await client.PostAsJsonAsync("api/users/login", userEnvelope);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(body);
        }
        response.EnsureSuccessStatusCode();
        var userResponse = await response.Content.ReadFromJsonAsync<UserEnvelope<UserDto>>();

        userResponse.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userResponse.User.Token);
    }

    public async Task InitializeAsync()
    {
        client = new HttpClient();
        client.BaseAddress = new Uri("http://localhost:8081");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task DisposeAsync()
    {
        await Task.Yield();
    }
}
