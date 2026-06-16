using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlexCms.InvestPro.Data;

/// <summary>
/// Design-time factory used by the EF tooling (<c>dotnet ef migrations add ...</c>)
/// when generating migration files. Runtime activation never goes through this —
/// the framework's <see cref="InvestProModule.CreateMigrationContext"/>
/// constructs the same context against the configured production connection string.
///
/// <para>
/// We pick PostgreSQL here because it's the project's development backend.
/// EF tooling does not actually connect during <c>migrations add</c>, the
/// connection string is only used to satisfy the provider initialization.
/// </para>
/// </summary>
public sealed class InvestProDbContextDesignFactory : IDesignTimeDbContextFactory<InvestProDbContext>
{
    public InvestProDbContext CreateDbContext(string[] args)
    {
        const string ConnStr = "Host=localhost;Port=5432;Database=flexcms;Username=dev;Password=Dev@123456;";

        var options = new DbContextOptionsBuilder<InvestProDbContext>()
            .UseNpgsql(ConnStr)
            .Options;

        return new InvestProDbContext(options);
    }
}
