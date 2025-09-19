using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GRCFinancialControl.Data
{
    public static class AppDbCompiledModels
    {
        private static readonly Lazy<IModel?> _model = new(() => LoadCompiledModel());

        public static IModel? TryGetModel() => _model.Value;

        private static IModel? LoadCompiledModel()
        {
            const string compiledTypeName = "GRCFinancialControl.Data.CompiledModels.AppDbContextModel";
            var type = Type.GetType(compiledTypeName);
            if (type == null)
            {
                return null;
            }

            var instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty?.GetValue(null) is IModel instance)
            {
                return instance;
            }

            if (Activator.CreateInstance(type) is IModel created)
            {
                return created;
            }

            return null;
        }
    }
}
