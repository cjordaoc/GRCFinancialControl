using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Persistence;

internal static class PropertyBuilderExtensions
{
    public static PropertyBuilder<TProperty> HasMySqlColumnType<TProperty>(
        this PropertyBuilder<TProperty> builder,
        string columnType,
        bool isMySql)
    {
        if (isMySql)
        {
            builder.HasColumnType(columnType);
        }

        return builder;
    }

    public static PropertyBuilder<TProperty> HasMySqlDefaultValueSql<TProperty>(
        this PropertyBuilder<TProperty> builder,
        string sql,
        bool isMySql)
    {
        if (isMySql)
        {
            builder.HasDefaultValueSql(sql);
        }

        return builder;
    }
}
