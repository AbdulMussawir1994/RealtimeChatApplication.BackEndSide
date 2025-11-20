namespace App.Core.Interface;

public interface IConnectionManager
{
    void AddConnection(string userId, string connectionId);
    void RemoveConnection(string userId, string connectionId);
    IReadOnlyList<string> GetConnections(string userId);
    bool IsOnline(string userId);
}
