namespace zaaerIntegration.Services.Integrations.Zatca
{
    public interface IZatcaGatewayClient
    {
        Task<ZatcaSubmissionResult> SubmitAsync(
            ZatcaSubmissionRequest request,
            string csid,
            string secret,
            CancellationToken cancellationToken = default);
    }
}
