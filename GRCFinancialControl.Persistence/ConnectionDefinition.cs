namespace GRCFinancialControl.Persistence
{
    public sealed class ConnectionDefinition
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public uint Port { get; set; } = 3306;
        public string Database { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseSsl { get; set; } = true;

        public override string ToString()
        {
            return Name;
        }

        public ConnectionDefinition Clone()
        {
            return new ConnectionDefinition
            {
                Id = Id,
                Name = Name,
                Server = Server,
                Port = Port,
                Database = Database,
                Username = Username,
                Password = Password,
                UseSsl = UseSsl
            };
        }
    }
}
