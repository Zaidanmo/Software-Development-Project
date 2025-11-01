using Realworlddotnet.Core.Repositories;

namespace Realworlddotnet.Api.Features.Images;

public class ImagesHandler : IImagesHandler
{
    private static readonly HashSet<string> AllowedExt =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

    private readonly IWebHostEnvironment _env;
    private readonly IConduitRepository  _repo;

    public ImagesHandler(IWebHostEnvironment env, IConduitRepository repo)
    {
        _env  = env;
        _repo = repo;
    }

    private string ProfileFolder =>
        Path.Combine(_env.ContentRootPath, "Images", "ProfilePictures");

    // ─────────────────────────────────────── Profile Pictures Image Handling ───────────────────────────────────────
    public async Task<IResult> HandleAsync(
        HttpContext      ctx,
        IFormFile        image,
        CancellationToken cancellationToken)
    {
        // -------------------------- validations --------------------------
        var ext = Path.GetExtension(image.FileName);
        if (!AllowedExt.Contains(ext))
            return Results.BadRequest("Nur .jpg, .jpeg oder .png erlaubt.");
        if (image.Length == 0)
            return Results.BadRequest("Leere Datei.");

        // -------------------------- deterministic save-path --------------------------
        Directory.CreateDirectory(ProfileFolder);

        var newName  = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(ProfileFolder, newName);

        await using (var fs = new FileStream(fullPath, FileMode.Create))
        {
            await image.CopyToAsync(fs, cancellationToken);
        }

        // -------------------------- user lookup & old-file cleanup --------------------------
        var username = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (username is null) return Results.Unauthorized();

        var user = await _repo.GetUserByUsernameAsync(username, cancellationToken);
        if (user is null) return Results.Unauthorized();

        if (!string.IsNullOrWhiteSpace(user.Image))
        {
            var oldPath = Path.Combine(ProfileFolder, Path.GetFileName(user.Image));
            if (File.Exists(oldPath)) File.Delete(oldPath);
        }

        // -------------------------- persist & respond --------------------------
        var publicUrl =
            $"{ctx.Request.Scheme}://{ctx.Request.Host}/api/images/{newName}";

        user.Image = publicUrl;
        await _repo.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { url = publicUrl });
    }
    
    // ─────────────────────────────────────── Article Pictures Image Handling ───────────────────────────────────────

    private string ArticleFolder => 
        Path.Combine(_env.ContentRootPath, "Images", "Articles");
    
    public async Task<IResult> HandleArticleAsync(
        HttpContext ctx,
        string slug,
        IFormFileCollection files,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return Results.BadRequest("Keine Dateien hochgeladen.");
        
        var article = await _repo.GetArticleBySlugAsync(slug, false ,cancellationToken);
        
        if (article is null)
            return Results.NotFound();
        
        Directory.CreateDirectory(ArticleFolder);    
        
        var urls = new List<string>();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExt.Contains(ext)) continue;        // skip invalid
            
            var name = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(ArticleFolder, name);
            
            await using var fs = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(fs, cancellationToken);
            
            var url = $"{ctx.Request.Scheme}://{ctx.Request.Host}/api/images/articles/{name}";            
            article.Images.Add(new ArticleImage { Url = url });
            urls.Add(url);
        }
        await _repo.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { urls });
    }
}
