namespace ResearchAgentNetwork.Web;

public record RegisterRequest(string UserName, string Email, string Password, string? DisplayName);
public record LoginRequest(string UserName, string Password);
public record RefreshRequest(string RefreshToken);

