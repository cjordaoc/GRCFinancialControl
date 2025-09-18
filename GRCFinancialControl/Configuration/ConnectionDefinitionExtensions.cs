using System;
using GRCFinancialControl.Persistence;

namespace GRCFinancialControl.Configuration
{
    public static class ConnectionDefinitionExtensions
    {
        public static AppConfig ToAppConfig(this ConnectionDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return new AppConfig
            {
                Server = definition.Server,
                Port = definition.Port,
                Database = definition.Database,
                Username = definition.Username,
                Password = definition.Password,
                UseSsl = definition.UseSsl
            };
        }
    }
}
