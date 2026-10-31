using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class SupportWorkshopCatalogPostgreSqlTests
{
    [Fact]
    public async Task Operator_catalog_query_matches_real_migrated_PostgreSql_schema()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        await db.Database.MigrateAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            BEGIN READ ONLY;
            SELECT count(*) FILTER (WHERE lower("Type")='support') || '|' ||
                   count(*) FILTER (WHERE lower("Type")='support' AND "Number"='00') || '|' ||
                   count(*) FILTER (WHERE lower("Type")='support' AND "Number"='00'
                     AND "Id"='b12c0000-0000-4000-8000-000000000000'::uuid) || '|' ||
                   count(*) FILTER (WHERE "Number"='00' AND lower("Type")<>'support')
            FROM core.organizations;
            SELECT count(*) FROM pg_index i
            JOIN pg_class c ON c.oid=i.indexrelid
            WHERE i.indrelid='core.organizations'::regclass
              AND c.relname='UX_organizations_single_support'
              AND i.indisunique AND i.indisvalid AND i.indisready
              AND i.indnatts=1 AND i.indnkeyatts=1
              AND regexp_replace(pg_get_expr(i.indexprs,i.indrelid), '\s', '', 'g')='lower(("Type")::text)'
              AND regexp_replace(pg_get_expr(i.indpred,i.indrelid), '\s', '', 'g')='(lower(("Type")::text)=''support''::text)';
            SELECT count(*) FROM pg_constraint
            WHERE conrelid='core.organizations'::regclass
              AND conname='CK_organizations_support_number_00' AND contype='c' AND convalidated
              AND regexp_replace(pg_get_constraintdef(oid), '\s', '', 'g')=
                'CHECK((((lower(("Type")::text)=''support''::text)AND(("Number")::text=''00''::text))OR((lower(("Type")::text)<>''support''::text)AND(("Number")::textISDISTINCTFROM''00''::text))))';
            COMMIT;
            """;
        await using var reader = await command.ExecuteReaderAsync(ct);
        Assert.True(await reader.ReadAsync(ct));
        Assert.Equal("1|1|1|0", reader.GetString(0));
        Assert.True(await reader.NextResultAsync(ct));
        Assert.True(await reader.ReadAsync(ct));
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.True(await reader.NextResultAsync(ct));
        Assert.True(await reader.ReadAsync(ct));
        Assert.Equal(1L, reader.GetInt64(0));
    }
}
