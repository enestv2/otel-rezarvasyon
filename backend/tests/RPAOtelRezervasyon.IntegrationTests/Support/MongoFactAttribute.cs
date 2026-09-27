namespace RPAOtelRezervasyon.IntegrationTests.Support;

/// <summary>
/// Yalnızca <c>MONGO_TEST_CONNECTION</c> tanımlıysa çalışan isteğe bağlı entegrasyon testi.
/// MongoDB yoksa test "skipped" olarak raporlanır; `scripts/check` bu testlere bağlı değildir
/// (bkz. ADR-0002, plan 0001 madde 8).
/// </summary>
public sealed class MongoFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "MONGO_TEST_CONNECTION";

    public MongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = $"{ConnectionVariable} tanımlı değil; MongoDB entegrasyon testi atlandı.";
        }
    }
}
