namespace IntegrationTests.Infrastructure;

/// <summary>
/// Facts about the v1 endpoint inventory that more than one guard test asserts on.
/// </summary>
internal static class ApiInventory
{
    /// <summary>
    /// Operations in the v1 surface, per the inventory in <c>ROADMAP/00-overview.md</c>.
    /// Both the OpenAPI contract guard and the <c>http/</c> coverage guard pin this number,
    /// so it lives here rather than being bumped in two files per new endpoint.
    /// </summary>
    public const int OperationCount = 43;
}
