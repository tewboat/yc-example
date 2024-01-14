namespace App.Configuration;

internal sealed class YdbConfig
{
    public string Endpoint { get; init; } = "grpcs://ydb.serverless.yandexcloud.net:2135";

    public string Database { get; init; } = "/ru-central1/b1g6hi43r2dnmslajhhg/etni09pht4mjpbje4g14";
}