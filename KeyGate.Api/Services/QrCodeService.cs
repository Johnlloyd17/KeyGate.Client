using QRCoder;

namespace KeyGate.Api.Services;

public class QrCodeService
{
    public byte[] GenerateQrCodePng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(20);
    }
}
