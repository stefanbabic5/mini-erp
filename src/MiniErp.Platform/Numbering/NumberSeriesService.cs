using MiniErp.Platform.Security;
using Npgsql;

namespace MiniErp.Platform.Numbering;

/// <summary>Hands out document numbers for the current tenant.</summary>
public interface INumberSeriesService
{
    /// <summary>
    /// The next number of a series where gaps are allowed (customers, orders, shipments; ADR-0009).
    /// It is taken in its own short transaction, so no lock is held while the caller works, and a
    /// caller that fails afterwards leaves a gap. Never returns the same number twice.
    /// </summary>
    Task<string> NextAsync(string code, CancellationToken cancellationToken);
}

internal sealed class NumberSeriesService(NpgsqlDataSource dataSource, ITenantContext tenantContext) : INumberSeriesService
{
    // One atomic statement: create the tenant's series on first use with its default format,
    // otherwise increment it. PostgreSQL locks the row only for this statement, so concurrent
    // callers queue for microseconds and each gets a different number.
    // Raw SQL bypasses EF Core's tenant filter, so the tenant is part of the statement (ADR-0003).
    private const string NextNumberSql = """
        INSERT INTO platform.number_series (tenant_id, code, prefix, digits, last_no, updated_at)
        VALUES (@tenant_id, @code, @prefix, @digits, 1, now())
        ON CONFLICT (tenant_id, code)
        DO UPDATE SET last_no = number_series.last_no + 1, updated_at = now()
        RETURNING prefix, digits, last_no;
        """;

    public async Task<string> NextAsync(string code, CancellationToken cancellationToken)
    {
        if (!NumberSeriesCodes.Defaults.TryGetValue(code, out var defaults))
        {
            throw new ArgumentException($"Unknown number series '{code}'.", nameof(code));
        }

        await using var command = dataSource.CreateCommand(NextNumberSql);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("prefix", defaults.Prefix);
        command.Parameters.AddWithValue("digits", defaults.Digits);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return NumberSeries.Format(
            prefix: reader.GetString(0),
            digits: reader.GetInt32(1),
            number: reader.GetInt64(2));
    }
}
