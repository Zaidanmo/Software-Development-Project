namespace Realworlddotnet.Core.Dto;

public record SearchQuery(string? query, int Limit=20, int Offset=0);
