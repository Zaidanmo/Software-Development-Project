using Microsoft.AspNetCore.Mvc;
using Realworlddotnet.Core.Repositories;

namespace Realworlddotnet.Api.Features.Images;

public sealed class ImagesModule : ICarterModule
{
    private static string GetProfileFolder(IWebHostEnvironment env) =>
        Path.Combine(env.ContentRootPath, "Images", "ProfilePictures");

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var profileGroup = app.MapGroup("/images") //for profile-picture images
            .WithTags("Images")
            .IncludeInOpenApi();
        
        var articleGroup = app.MapGroup("/articles/{slug}/images") //for article images
            .WithTags("Article Images")
            .IncludeInOpenApi();

        // ---------- GET /api/images/{fileName} ----------
        
        profileGroup.MapGet("/{fileName}", (string fileName, IWebHostEnvironment env) =>
            {
                var fullPath = Path.Combine(GetProfileFolder(env), fileName);

                if (!File.Exists(fullPath))
                    return Results.NotFound();

                var mime = Path.GetExtension(fullPath).Equals(".png",
                    StringComparison.OrdinalIgnoreCase)
                    ? "image/png"
                    : "image/jpeg";

                return Results.File(fullPath, mime);
            })
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // ---------- POST /api/images  (field 'image') ----------
        
        profileGroup.MapPost("/", [Authorize] async (
                    HttpContext ctx,
                    [FromForm(Name = "image")] IFormFile image,
                    IImagesHandler handler,         
                    CancellationToken cancellationToken)
                => await handler.HandleAsync(ctx, image, cancellationToken))
            .Accepts<IFormFile>("multipart/form-data")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        // ---------- POST /api/articles/{slug}/images ----------
        
        articleGroup.MapPost("/",
                [Authorize] async (
                    string slug,
                    HttpContext ctx,
                    IImagesHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    return await handler.HandleArticleAsync(ctx, slug, ctx.Request.Form.Files, cancellationToken);
                })
            .Accepts<IFormFile>("multipart/form-data")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
        
        // ---------- GET /api/articles/{slug}/images ----------

        app.MapGet("/articles/{slug}/images",
                async (string slug, IConduitRepository repo, CancellationToken ct) =>
                {
                    var article = await repo.GetArticleBySlugAsync(slug, true, ct);
                    if (article is null) return Results.NotFound();

                    var urls = article.Images.Select(i => i.Url).ToList();
                    return Results.Ok(new { urls });
                })
            .WithTags("Article Images")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
        
        // ---------- GET  /api/images/articles/{fileName}  (raw bytes) ----------
        
        app.MapGet("/images/articles/{fileName}",
                (string fileName, IWebHostEnvironment env) =>
                {
                    var fullPath = Path.Combine(
                        env.ContentRootPath,         
                        "Images", "Articles",      
                        fileName);

                    if (!System.IO.File.Exists(fullPath))
                        return Results.NotFound();

                    var mime = Path.GetExtension(fullPath).ToLowerInvariant() switch
                    {
                        ".png"        => "image/png",
                        ".jpg" or ".jpeg"       => "image/jpeg",
                        _             => "application/octet-stream"
                    };
                    return Results.File(fullPath, mime);
                })
            .AllowAnonymous()
            .WithTags("Article Images")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DownloadArticleImage");
    }
}
