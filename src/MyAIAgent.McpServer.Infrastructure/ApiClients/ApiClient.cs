using System.Net.Http.Json;
using MyAIAgent.McpServer.Domain.Interfaces;

namespace MyAIAgent.McpServer.Infrastructure.ApiClients;

public class ApiClient<T>(HttpClient httpClient) : IApiClient<T> where T : class
{
    public async Task<List<T>> GetAsync(string action, CancellationToken ct)
    {
        var data = await httpClient.GetFromJsonAsync<List<T>>(action, ct);
        return data ?? [];
    }

    public async Task<T?> GetByIdAsync(string action, int id, CancellationToken ct)
    {
        var data = await httpClient.GetFromJsonAsync<T>($"{action}/{id}", ct);
        return data;
    }

    public async Task<T> CreateAsync(string action, T newEntity, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(action, newEntity, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct)
               ?? throw new HttpRequestException("Failed to deserialize created entity.");
    }

    public async Task<T> UpdateAsync(string action, int id, T updatedEntity, CancellationToken ct)
    {
        var response = await httpClient.PutAsJsonAsync($"{action}/{id}", updatedEntity, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct)
               ?? throw new HttpRequestException($"Failed to deserialize updated entity with ID '{id}'.");
    }

    public async Task DeleteAsync(string action, int id, CancellationToken ct)
    {
        var response = await httpClient.DeleteAsync($"{action}/{id}", ct);
        response.EnsureSuccessStatusCode();
    }
}