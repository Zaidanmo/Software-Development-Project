namespace Realworlddotnet.Core.Entities;

// Data/Entities/ArticleImage.cs
public class ArticleImage
{
    public Guid     Id         { get; set; }
    public string   Url        { get; set; } = default!;
    public Guid     ArticleId  { get; set; }
    public Article  Article    { get; set; } = default!;
}
