using System;
using Microsoft.Xrm.Sdk;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

internal static class EntityAttributeExtensions
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
