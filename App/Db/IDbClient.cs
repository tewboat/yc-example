namespace App.Db;

internal interface IDbClient
{
    Task CreateTableAsync(CancellationToken cancellationToken = default);
        
    Task<IReadOnlyCollection<Record>> GetAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(string recordText, CancellationToken cancellationToken = default);
}