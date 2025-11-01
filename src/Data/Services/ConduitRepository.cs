using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hellang.Middleware.ProblemDetails;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Realworlddotnet.Core.Dto;
using Realworlddotnet.Core.Entities;
using Realworlddotnet.Core.Repositories;
using Realworlddotnet.Data.Contexts;
using Serilog;

namespace Realworlddotnet.Data.Services;

public class ConduitRepository : IConduitRepository
{
    private readonly ConduitContext _context;

    public ConduitRepository(ConduitContext context)
    {
        _context = context;
    }

    public async Task AddUserAsync(User user)
    {
        if (await _context.Users.AnyAsync(x => x.Username == user.Username))
        {
            throw new ProblemDetailsException(new ValidationProblemDetails
            {
                Status = 422,
                Detail = "Cannot register user",
                Errors = { new KeyValuePair<string, string[]>("Username", new[] { "Username not available" }) }
            });
        }

        if (await _context.Users.AnyAsync(x => x.Email == user.Email))
        {
            throw new ProblemDetailsException(new ValidationProblemDetails
            {
                Status = 422,
                Detail = "Cannot register user",
                Errors = { new KeyValuePair<string, string[]>("Email", new[] { "Email address already in use" }) }
            });
        }

        _context.Users.Add(user);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users/*.AsNoTracking()*/.FirstOrDefaultAsync(x => x.Email == email);
    }

    public async Task<List<ArticleFavorite>> GetLikedArticles()
    {
        return await _context.ArticleFavorites.Select(x => x).AsNoTracking().ToListAsync();
    }

    public async Task<List<Comment>> GetAllComments()
    {
        return await _context.Comments.Select(x => x).AsNoTracking().ToListAsync();
    }

    
    public async Task<List<UserLink>> GetAllFollowedUsers()
    {
        return await _context.FollowedUsers.Select(x => x).AsNoTracking().ToListAsync();
    }
    

    public Task<User> GetUserByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return _context.Users.FirstAsync(x => x.Username == username, cancellationToken);
    }

    public async Task<IEnumerable<Tag>> UpsertTagsAsync(IEnumerable<string> tags,
        CancellationToken cancellationToken)
    {
        var dbTags = await _context.Tags.Where(x => tags.Contains(x.Id)).ToListAsync(cancellationToken);

        return tags.Select(tag =>
            !dbTags.Exists(x => x.Id == tag) 
                ? _context.Tags.Add(new Tag(tag)).Entity 
                : _context.Tags.Find(tag)!
        );
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Boolean> IsArticleContainsKeyword(
        SearchQuery searchQuery,
        Article article,
        CancellationToken cancellationToken)
    {
        string[] keywords = searchQuery.query.Split(" ");
        
        var query = _context.Articles.Select(x => x); 
        query = ConductKeywordSearchWithArticle(keywords, article, query);
        int count = await query.CountAsync();
        return count > 0;
    }

    public async Task<ArticlesResponseDto> GetArticlesAsync(
        SearchQuery searchQuery,
        CancellationToken cancellationToken)
    {
        string[] arr_keywords = searchQuery.query.Split(" ");
        List<string> keywords = arr_keywords.ToList();
        
        // ordinary search
        IQueryable<Article> query = _context.Articles.Select(x => x); 
            
        // conduct search for keywords
        query = ConductKeywordSearch(keywords, query);

        var total = await query.CountAsync(cancellationToken);
        
        var pageQuery = query
            .Skip(searchQuery.Offset)
            .Take(searchQuery.Limit)
            .Include(x => x.Author)
            .Include(x => x.Tags)
            .Include(x => x.Images)
            .AsNoTracking();
        
        var page = await pageQuery.ToListAsync(cancellationToken);
        
        return new ArticlesResponseDto(page, total);
    }

    private IQueryable<Article> ConductKeywordSearchWithArticle(string[] keywords, Article article, IQueryable<Article> query)
    {
        // add keywords to query
        foreach(string kw in keywords)
        {
            if (!string.IsNullOrWhiteSpace(kw))
            {
                string kw_lower = kw.ToLower();
                query = query.Where(x => x.Id==article.Id && (x.Title.ToLower().Contains(kw_lower) || x.Body.ToLower().Contains(kw_lower) || x.Tags.Any(tag => tag.Id.ToLower().Contains(kw_lower))));
                //query = query.Where(x => x.Title.Contains(kw) || x.Body.Contains(kw) || x.Tags.Any(tag => tag.Id.Contains(kw)));
            }
        }
        query = query.OrderByDescending(x => x.UpdatedAt);
        return query;
    }
    
    private IQueryable<Article> ConductKeywordSearch(List<string> keywords, IQueryable<Article> query)
    {
        // add keywords to query
        foreach(string kw in keywords)
        {
            if (!string.IsNullOrWhiteSpace(kw))
            {
                string kw_lower = kw.ToLower();
                query = query.Where(x => x.Title.ToLower().Contains(kw_lower) || x.Body.ToLower().Contains(kw_lower) || x.Tags.Any(tag => tag.Id.ToLower().Contains(kw_lower)));
                //query = query.Where(x => x.Title.Contains(kw) || x.Body.Contains(kw) || x.Tags.Any(tag => tag.Id.Contains(kw)));
            }
        }
        query = query.OrderByDescending(x => x.UpdatedAt);
        return query;
    }

    public async Task<ArticlesResponseDto> GetArticlesAsync(
        ArticlesQuery articlesQuery,
        string? username,
        bool isFeed,
        CancellationToken cancellationToken)
    {
        var query = _context.Articles.Select(x => x);

        if (!string.IsNullOrWhiteSpace(articlesQuery.Author))
        {
            query = query.Where(x => x.Author.Username == articlesQuery.Author);
        }

        if (!string.IsNullOrWhiteSpace(articlesQuery.Tag))
        {
            query = query.Where(x => x.Tags.Any(tag => tag.Id == articlesQuery.Tag));
        }

        query = query.Include(x => x.Author);

        if (username is not null)
        {
            query = query.Include(x => x.Author)
                .ThenInclude(x => x.Followers.Where(fu => fu.FollowerUsername == username))
                .AsSplitQuery();
        }

        if (isFeed)
        {
            query = query.Where(x => x.Author.Followers.Any());
        }

        query = articlesQuery.Sort switch
        {
            ArticleSort.ByTitle => query.OrderBy(article => article.Title),
            ArticleSort.ByTagCount => query.OrderByDescending(article => article.Tags.Count),
            null => query.OrderByDescending(x => x.UpdatedAt),
            _ => throw new ArgumentOutOfRangeException()
        };
        //query = query.OrderByDescending(x => x.UpdatedAt);

        var total = await query.CountAsync(cancellationToken);
        
        var pageQuery = query
            .Skip(articlesQuery.Offset).Take(articlesQuery.Limit)
            .Include(x => x.Author)
            .Include(x => x.Tags)
            .Include(x => x.Images)
            .AsNoTracking();

        var page = await pageQuery.ToListAsync(cancellationToken);

        return new ArticlesResponseDto(page, total);
    }

    public async Task<Article?> GetArticleBySlugAsync(string slug, bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var query = _context.Articles
            .Include(x => x.Author)
            .Include(x => x.Tags)
            .Include(x => x.Images);

        var article = await query
            .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);

        if (asNoTracking)
        {
            query.AsNoTracking();
        }

        if (article == null)
        {
            return article;
        }

        if (!asNoTracking)
        {
            article.ReadCount += 1;
            _context.Entry(article).Property(a => a.ReadCount).IsModified = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var favoriteCount = await _context.ArticleFavorites.CountAsync(x => x.ArticleId == article.Id);
        article.Favorited = favoriteCount > 0;
        article.FavoritesCount = favoriteCount;
        return article;
    }

    public void AddArticle(Article article)
    {
        _context.Articles.Add(article);
    }

    public async Task DeleteArticle(Article article)
    {
        await Task.Yield();
        _context.Articles.Remove(article);
    }

    public async Task<ArticleFavorite?> GetArticleFavoriteAsync(string username, Guid articleId)
    {
        return await _context.ArticleFavorites.FirstOrDefaultAsync(x =>
            x.Username == username && x.ArticleId == articleId);
    }

    public void AddArticleFavorite(ArticleFavorite articleFavorite)
    {
        _context.ArticleFavorites.Add(articleFavorite);
    }

    public void AddArticleComment(Comment comment)
    {
        _context.Comments.Add(comment);
    }

    public void RemoveArticleComment(Comment comment)
    {
        _context.Comments.Remove(comment);
    }

    public async Task<List<Comment>> GetCommentsBySlugAsync(string slug, string? username,
        CancellationToken cancellationToken)
    {
        return await _context.Comments.Where(x => x.Article.Slug == slug)
            .Include(x => x.Author)
            .ThenInclude(x => x.Followers.Where(fu => fu.FollowerUsername == username))
            .ToListAsync(cancellationToken);
    }

    public void RemoveArticleFavorite(ArticleFavorite articleFavorite)
    {
        _context.ArticleFavorites.Remove(articleFavorite);
    }

    public Task<List<Tag>> GetTagsAsync(CancellationToken cancellationToken)
    {
        Task<List<Tag>> result = null;
        while (true)
        {
            try
            {
                result = _context.Tags.AsNoTracking().ToListAsync(cancellationToken);
                break;
            }
            catch (SqliteException e)
            {
                Thread.Sleep(100);   
            }
        }
        return result;
    }

    public Task<bool> IsFollowingAsync(string username, string followerUsername, CancellationToken cancellationToken)
    {
        return _context.FollowedUsers.AnyAsync(
            x => x.Username == username && x.FollowerUsername == followerUsername,
            cancellationToken);
    }

    public void Follow(string username, string followerUsername)
    {
        _context.FollowedUsers.Add(new UserLink(username, followerUsername));
    }

    public void UnFollow(string username, string followerUsername)
    {
        _context.FollowedUsers.Remove(new UserLink(username, followerUsername));
    }
    
    public async Task<IEnumerable<SearchCount>> UpsertKeywordCountsAsync(IEnumerable<string> keywords,
        CancellationToken cancellationToken)
    {
        var dbKeywords = await _context.SearchCount.Where(x => keywords.Contains(x.KeywordId)).ToListAsync(cancellationToken);
        
        List<SearchCount> scs = new List<SearchCount>();
        
        foreach(var kw in keywords)
        {
            if (!dbKeywords.Exists(x => x.KeywordId == kw))
            {
                scs.Add(_context.SearchCount.Add(new SearchCount(kw)).Entity);
            }
            else
            {
                scs.Add(_context.SearchCount.Find(kw).inc());
            }
        }
        
        return scs;
    }

    public async Task<IEnumerable<SearchCount>> GetKeywordsWithMinCount(int minCount, CancellationToken cancellationToken)
    {
        // get all keywords
        return await _context.SearchCount.Where(x => x.Count>= minCount).ToListAsync(cancellationToken);
    }
    
}
