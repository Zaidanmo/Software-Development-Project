using Microsoft.EntityFrameworkCore.Query;

namespace Realworlddotnet.Core.Entities;

public class SearchCount
{
    public SearchCount(string keywordId)
    {
        KeywordId = keywordId;
        Count = 1;
    }

    public SearchCount inc()
    {
        Count++;
        return this;
    }

    public override string ToString()
    {
        return "SC(" + KeywordId + ", #" + Count + ")";
    }
    
    public string KeywordId { get; set; }  
    public int Count { get; set; }
}
