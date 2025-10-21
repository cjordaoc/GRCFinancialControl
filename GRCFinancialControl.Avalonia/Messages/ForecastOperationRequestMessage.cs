namespace GRCFinancialControl.Avalonia.Messages
{
    public enum ForecastOperationRequestType
    {
        Refresh,
        GenerateTemplateRetain,
        ExportPending
    }

    public sealed record ForecastOperationRequestMessage(ForecastOperationRequestType Operation);
}
