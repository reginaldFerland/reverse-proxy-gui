namespace ReverseProxy.Models;

public class Mapping
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoutePattern { get; set; } = string.Empty;
    public string Destination1 { get; set; } = string.Empty;
    public string Destination2 { get; set; } = string.Empty;
    public int ActiveDestination { get; set; } = 1; // 1 for Destination1, 2 for Destination2
}