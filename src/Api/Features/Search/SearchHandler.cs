using Microsoft.Identity.Client;
using Realworlddotnet.Core.Dto;
using Realworlddotnet.Core.Repositories;

namespace Realworlddotnet.Api.Features.Search;

public class SearchHandler : ISearchHandler
{
    private readonly IConduitRepository _repository;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    

    public SearchHandler(IConduitRepository repository)
    {
        _repository = repository;
    }

    public async Task<(ArticlesResponseDto,IEnumerable<SearchCount>)> GetArticlesAsync(SearchQuery query, CancellationToken cancellationToken)
    {
        var searchResult = await this._repository.GetArticlesAsync(query, cancellationToken);
        
        string[] keywords = query.query.Split(" ");
        var res = await this._repository.UpsertKeywordCountsAsync(keywords, cancellationToken);
        
        await this._repository.SaveChangesAsync(cancellationToken);
        
        return (searchResult, res);
    }
    
   
}
