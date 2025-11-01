namespace Realworlddotnet.Api.Features.Images;

public interface IImagesHandler
{
    Task<IResult> HandleAsync(HttpContext ctx, IFormFile image, CancellationToken cancellationToken);
    
    Task<IResult> HandleArticleAsync(HttpContext ctx, string slug, IFormFileCollection files, CancellationToken cancellationToken);
}
