using System.Collections;
using Realworlddotnet.Api.Features.Articles;
using Realworlddotnet.Core.Dto;

namespace Realworlddotnet.Api.Features.Search;

public class SearchModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var authorizedGroup = app.MapGroup("search")
                .RequireAuthorization()
                .WithTags("Search")
                .IncludeInOpenApi()
            ;
  
            
            authorizedGroup.MapGet("/",
                    async Task<Ok<ArticlesResponse>>
                    ([AsParameters] SearchQuery Query,
                        ISearchHandler searchHandler,
                        ClaimsPrincipal claimsPrincipal)
                            =>
                    {
                        var response = await searchHandler.GetArticlesAsync(Query, new CancellationToken());
                        var result = ArticlesMapper.MapFromArticles(response.Item1);
                        return TypedResults.Ok(result);                
                    }
            )
            .WithName("Search");
    }
}
