namespace App.Db;

using Ydb.Sdk;
using Ydb.Sdk.Services.Table;
using Ydb.Sdk.Value;

internal sealed class DbClient : IDbClient
{
    private readonly Driver driver;

    public DbClient(Driver driver)
    {
        this.driver = driver;
    }

    public async Task CreateTableAsync(CancellationToken cancellationToken = default)
    {
        var client = new TableClient(driver);
        var response = await client.SessionExec(async session =>
        {
            return await session.ExecuteSchemeQuery(@"
                    CREATE TABLE records (
                        record_id string NOT NULL,
                        text Utf8,
                        PRIMARY KEY (record_id)
                    );
                ");
        });

        response.Status.EnsureSuccess();
    }

    public async Task<IReadOnlyCollection<Record>> GetAsync(CancellationToken cancellationToken = default)
    {
        var client = new TableClient(driver);
        var response = await client.SessionExec(async session =>
        {
            var query = "SELECT * FROM records";

            return await session.ExecuteDataQuery(
                query: query,
                txControl: TxControl.BeginSerializableRW().Commit(),
                parameters: new Dictionary<string, YdbValue>()
            );
        });

        response.Status.EnsureSuccess();
        var queryResponse = (ExecuteDataQueryResponse)response;
        var resultSet = queryResponse.Result.ResultSets[0];
        
        return resultSet.Rows.Select(row => new Record(row[1].GetOptionalUtf8()!)).ToList();
    }

    public async Task CreateAsync(string recordText, CancellationToken cancellationToken = default)
    {
        var client = new TableClient(driver);
        var response = await client.SessionExec(async session =>
        {
            var query = @"
                    DECLARE $id AS string;
                    DECLARE $text AS Utf8;

                    UPSERT INTO records (record_id, text) VALUES
                        ($id, $text);";

            return await session.ExecuteDataQuery(
                query: query,
                txControl: TxControl.BeginSerializableRW().Commit(),
                parameters: new Dictionary<string, YdbValue>
                {
                    { "$id", YdbValue.MakeString(Guid.NewGuid().ToByteArray()) },
                    { "$text", YdbValue.MakeUtf8(recordText) }
                }
            );
        });

        response.Status.EnsureSuccess();
    }
}