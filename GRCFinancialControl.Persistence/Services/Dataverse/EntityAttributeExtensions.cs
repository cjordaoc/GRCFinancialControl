using System;
using Microsoft.Xrm.Sdk;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

public static class EntityAttributeExtensions
{
    public static int GetInt(this Entity entity, string attribute, int defaultValue = 0)
    {
        if (entity.TryGetAttributeValue(attribute, out int value))
        {
            return value;
        }

        if (entity.TryGetAttributeValue(attribute, out OptionSetValue? optionSet) && optionSet is not null)
        {
            return optionSet.Value;
        }

        return defaultValue;
    }

    public static string GetString(this Entity entity, string attribute)
    {
        if (entity.TryGetAttributeValue(attribute, out string? value) && value is not null)
        {
            return value;
        }

        return string.Empty;
    }

    public static string? GetOptionalString(this Entity entity, string attribute)
    {
        if (entity.TryGetAttributeValue(attribute, out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return null;
    }

    public static decimal GetDecimal(this Entity entity, string attribute, decimal defaultValue = 0m)
    {
        if (entity.TryGetAttributeValue(attribute, out decimal value))
        {
            return value;
        }

        if (entity.TryGetAttributeValue(attribute, out Money? money) && money is not null)
        {
            return money.Value;
        }

        return defaultValue;
    }

    public static decimal? GetNullableDecimal(this Entity entity, string attribute)
    {
        if (entity.Attributes.TryGetValue(attribute, out var raw))
        {
            return raw switch
            {
                decimal value => value,
                Money money => money.Value,
                _ => null,
            };
        }

        return null;
    }

    public static DateTime? GetDateTime(this Entity entity, string attribute)
    {
        if (entity.TryGetAttributeValue(attribute, out DateTime value))
        {
            return DateTime.SpecifyKind(value, value.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : value.Kind);
        }

        if (entity.TryGetAttributeValue(attribute, out DateTime? nullable) && nullable.HasValue)
        {
            var nullableValue = nullable.Value;
            return DateTime.SpecifyKind(nullableValue, nullableValue.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : nullableValue.Kind);
        }

        return null;
    }

    public static int? GetNullableInt(this Entity entity, string attribute)
    {
        if (entity.TryGetAttributeValue(attribute, out int value))
        {
            return value;
        }

        if (entity.TryGetAttributeValue(attribute, out OptionSetValue? option) && option is not null)
        {
            return option.Value;
        }

        return null;
    }

    public static int? GetAliasedInt(this Entity entity, string alias, string attribute)
    {
        var raw = GetAliasedRaw(entity, alias, attribute);
        return raw switch
        {
            int value => value,
            long longValue => (int)longValue,
            _ => null,
        };
    }

    public static string? GetAliasedString(this Entity entity, string alias, string attribute)
    {
        return GetAliasedRaw(entity, alias, attribute) as string;
    }

    private static object? GetAliasedRaw(Entity entity, string alias, string attribute)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var key = string.Concat(alias, ".", attribute);
        if (entity.Attributes.TryGetValue(key, out var raw) && raw is AliasedValue aliased)
        {
            return aliased.Value;
        }

        return null;
    }

    public static bool TryGetAttributeValue<T>(this Entity entity, string attribute, out T value)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.Attributes.TryGetValue(attribute, out var raw) && raw is T castValue)
        {
            value = castValue;
            return true;
        }

        value = default!;
        return false;
    }
}
