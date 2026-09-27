namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>
/// Üretilmiş belge: ikili içerik, içerik türü ve dosya adı (R-1). Çekirdek, belgeyi üreten
/// kütüphaneyi bilmez; yalnızca bu sonucu taşır.
/// </summary>
public sealed record ReportDocument
{
    public const string PdfContentType = "application/pdf";

    public ReadOnlyMemory<byte> Content { get; }

    public string ContentType { get; }

    public string FileName { get; }

    public ReportDocument(ReadOnlyMemory<byte> content, string contentType, string fileName)
    {
        if (content.IsEmpty)
        {
            throw new ArgumentException("Belge içeriği boş olamaz.", nameof(content));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        Content = content;
        ContentType = contentType;
        FileName = fileName;
    }

    public bool IsPdf => string.Equals(ContentType, PdfContentType, StringComparison.OrdinalIgnoreCase);
}