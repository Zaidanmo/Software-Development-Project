using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Realworlddotnet.Core.Dto;
using Realworlddotnet.Data.Contexts;
using Realworlddotnet.Data.Services;

namespace Assignment.Tests;

public class Assignment1 : IAsyncLifetime
{
    // Initialized in async lifetime
    // This can be shared because of its readonly connection
    private ConduitContext _context = null!;

    [Fact(DisplayName = "Assignment 1.1 - Database Check Articles"),
     Trait("Number", "1.1")]
    public async Task DatabaseCheckNumberArticles()
    {
        var repo = new ConduitRepository(_context);
        var (list, _) = await repo.GetArticlesAsync(new ArticlesQuery(null, null, null, Limit: -1),
            null, false, CancellationToken.None);

        list.Should().HaveCountGreaterOrEqualTo(200, "there should be at least 200 articles");
        list.Should().OnlyContain(article => article.Tags.Count >= 3, "each article should have at least 3 tags");
        list.Should().OnlyContain(article => article.Body.Split(' ', StringSplitOptions.TrimEntries).Length >= 50,
            "each article should have at least 50 words");
        list.Select(article => article.Author).Distinct().Should()
            .HaveCountGreaterOrEqualTo(20, "the articles should be by 20 different authors");

        // in markdown?
    }

    [Fact(DisplayName = "Assignment 1.2 - Database Check Likes"),
     Trait("Number", "1.2")]
    public async Task DatabaseCheckNumberLikesArticles()
    {
        
        var repo = new ConduitRepository(_context);
        var likedArticles = await repo.GetLikedArticles();

        likedArticles.DistinctBy(x => x.ArticleId).Should()
            .HaveCountGreaterOrEqualTo(50, "at least 50 articles with 1-5 likes");
    }

    [Fact(DisplayName = "Assignment 1.3 - Database Check Comments"),
     Trait("Number", "1.3")]
    public async Task DatabaseCheckNumberCommentsArticles()
    {
        
        var repo = new ConduitRepository(_context);
        var commentsResult = await repo.GetAllComments();

        commentsResult.DistinctBy(comment => comment.ArticleId).Should()
            .HaveCountGreaterOrEqualTo(20, "at least 20 articles with 1-3 comments");
    }

    [Fact(DisplayName = "Assignment 1.4 - Database Check Follower"),
     Trait("Number", "1.4")]
    public async Task DatabaseCheckAuthorFollows()
    {
        
        var repo = new ConduitRepository(_context);
        var authorsResult = await repo.GetAllFollowedUsers();

        authorsResult.DistinctBy(aut => aut.Username).Should()
            .HaveCountGreaterOrEqualTo(5, "at least 5 authors with 1-3 followers");
    }

    public async Task InitializeAsync()
    {
        var connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = "../../../../../realworld.db",
                Mode = SqliteOpenMode.ReadOnly  // tests should be deterministic & reproducible
            }.ToString();
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        var contextOptions = new DbContextOptionsBuilder<ConduitContext>()
            .LogTo(Console.WriteLine).EnableSensitiveDataLogging().EnableDetailedErrors()
            .UseSqlite(connection)
            .Options;

        var context = new ConduitContext(contextOptions);
        await context.Database.EnsureCreatedAsync();
        _context = context;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
