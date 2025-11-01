namespace Realworlddotnet.Core.Entities;

public class ArticleFavorite
{
    public ArticleFavorite(string username, Guid articleId)
    {
        Username = username;
        ArticleId = articleId;
        Timestamp = DateTime.UtcNow;
    }

    public string Username { get; set; }

    public Guid ArticleId { get; set; }

    public DateTime? Timestamp { get; set; }
    
    public User User { get; set; } = null!;

    public Article Article { get; set; } = null!;
}
