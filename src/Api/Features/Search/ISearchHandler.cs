using Realworlddotnet.Core.Dto;

namespace Realworlddotnet.Api.Features.Search;
public interface ISearchHandler
{
    public Task<(ArticlesResponseDto,IEnumerable<SearchCount>)> GetArticlesAsync(SearchQuery query, CancellationToken cancellationToken);
}
