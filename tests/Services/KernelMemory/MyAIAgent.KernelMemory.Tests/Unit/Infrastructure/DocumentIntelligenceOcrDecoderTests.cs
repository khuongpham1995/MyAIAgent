/**
 * MyAIAgent | KernelMemory · Tests
 *
 * Verifies image and PDF OCR fallback, page limits, and safe failure behavior.
 */

using FluentAssertions;
using Microsoft.KernelMemory.DataFormats;
using Moq;
using MyAIAgent.KernelMemory.Infrastructure.Models;
using MyAIAgent.KernelMemory.Infrastructure.Services;

namespace MyAIAgent.KernelMemory.Tests.Unit.Infrastructure;

public sealed class DocumentIntelligenceOcrDecoderTests
{
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/tiff")]
    [InlineData("image/bmp")]
    [InlineData("image/heif")]
    public void ImageDecoder_WhenMimeTypeIsSupported_ShouldClaimIt(string mimeType)
    {
        // Arrange
        var decoder = new DocumentIntelligenceImageDecoder(Mock.Of<IDocumentIntelligenceOcrClient>(), Options());

        // Act
        var supportsMimeType = decoder.SupportsMimeType(mimeType);

        // Assert
        supportsMimeType.Should().BeTrue();
        decoder.SupportsMimeType("text/plain").Should().BeFalse();
        decoder.SupportsMimeType("image/heic").Should().BeFalse();
    }

    [Fact]
    public async Task ImageDecoder_WhenHeifReturnsOnePage_ShouldKeepItsText()
    {
        // Arrange
        var client = new Mock<IDocumentIntelligenceOcrClient>();
        client.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OcrPage(1, "HEIF text")]);
        var decoder = new DocumentIntelligenceImageDecoder(client.Object, Options());

        // Act
        var result = await decoder.DecodeAsync(BinaryData.FromBytes(Heif()));

        // Assert
        result.Sections.Select(x => x.Content).Should().Equal("HEIF text");
    }

    [Fact]
    public async Task ImageDecoder_WhenHeifReturnsMultiplePages_ShouldRejectPartialImage()
    {
        // Arrange
        var client = new Mock<IDocumentIntelligenceOcrClient>();
        client.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OcrPage(1, "One"), new OcrPage(2, "Two")]);
        var decoder = new DocumentIntelligenceImageDecoder(client.Object, Options());

        // Act
        var action = () => decoder.DecodeAsync(BinaryData.FromBytes(Heif()));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PdfDecoder_WhenNativeTextExists_ShouldKeepItWithoutOcr()
    {
        // Arrange
        var native = Content("Existing PDF text", " ");
        var pdf = PdfDecoder(native, out var client);

        // Act
        var result = await pdf.DecodeAsync(new BinaryData("%PDF-test"));

        // Assert
        result.Sections.Select(x => x.Content).Should().Equal("Existing PDF text", " ");
        result.Sections[0].Number.Should().Be(1);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PdfDecoder_WhenEveryNativePageIsBlank_ShouldOcrAndPreservePageNumbers()
    {
        // Arrange
        var pdf = PdfDecoder(Content(" ", ""), out var client);
        client.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OcrPage> { new(1, "First page"), new(2, "Second page") });

        // Act
        var result = await pdf.DecodeAsync(new BinaryData("%PDF-test"));

        // Assert
        result.Sections.Select(x => (x.Number, x.Content)).Should()
            .Equal((1, "First page"), (2, "Second page"));
        client.Verify(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImageDecoder_WhenAzureReturnsText_ShouldPreservePageNumber()
    {
        // Arrange
        var client = new Mock<IDocumentIntelligenceOcrClient>();
        client.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OcrPage> { new(1, "Photo text") });
        var decoder = new DocumentIntelligenceImageDecoder(client.Object, Options());

        // Act
        var result = await decoder.DecodeAsync(BinaryData.FromBytes([1, 2, 3]));

        // Assert
        result.Sections.Should().ContainSingle();
        result.Sections[0].Number.Should().Be(1);
        result.Sections[0].Content.Should().Be("Photo text");
    }

    [Fact]
    public async Task ImageDecoder_WhenF0PayloadExceedsFourMiB_ShouldFailBeforeAzure()
    {
        // Arrange
        var client = new Mock<IDocumentIntelligenceOcrClient>();
        var decoder = new DocumentIntelligenceImageDecoder(client.Object, Options());

        // Act
        var action = () => decoder.DecodeAsync(BinaryData.FromBytes(new byte[4 * 1024 * 1024 + 1]));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PdfDecoder_WhenF0HasThreePages_ShouldFailBeforeAzure()
    {
        // Arrange
        var pdf = PdfDecoder(Content("", "", ""), out var client);

        // Act
        var action = () => pdf.DecodeAsync(new BinaryData("%PDF-test"));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImageDecoder_WhenF0TiffHasThreeFrames_ShouldFailBeforeAzure()
    {
        // Arrange
        var client = new Mock<IDocumentIntelligenceOcrClient>();
        var decoder = new DocumentIntelligenceImageDecoder(client.Object, Options());

        // Act
        var action = () => decoder.DecodeAsync(BinaryData.FromBytes(Tiff(3, littleEndian: true)));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tier limit*");
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ImageDecoder_WhenTiffHasTwoPages_ShouldOcrBothPages(bool littleEndian)
    {
        // Arrange
        var client = new Mock<IDocumentIntelligenceOcrClient>();
        client.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OcrPage> { new(1, "First"), new(2, "Second") });
        var decoder = new DocumentIntelligenceImageDecoder(client.Object, Options());

        // Act
        var result = await decoder.DecodeAsync(BinaryData.FromBytes(Tiff(2, littleEndian)));

        // Assert
        result.Sections.Select(x => (x.Number, x.Content)).Should().Equal((1, "First"), (2, "Second"));
        client.Verify(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImageDecoder_WhenTiffIfdChainIsInvalid_ShouldFailBeforeAzure(bool cyclic)
    {
        // Arrange
        var client = new Mock<IDocumentIntelligenceOcrClient>();
        var decoder = new DocumentIntelligenceImageDecoder(client.Object, Options());
        var tiff = Tiff(2, littleEndian: true);
        var pointer = cyclic ? 8 : 1000;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(tiff.AsSpan(16, 4), pointer);

        // Act
        var action = () => decoder.DecodeAsync(BinaryData.FromBytes(tiff));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*page count*");
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PdfDecoder_WhenS0HasThreePages_ShouldAnalyzeAllPages()
    {
        // Arrange
        var pdf = PdfDecoder(Content("", "", ""), out var client, "S0");
        client.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OcrPage> { new(1, "One"), new(2, "Two"), new(3, "Three") });

        // Act
        var result = await pdf.DecodeAsync(new BinaryData("%PDF-test"));

        // Assert
        result.Sections.Select(x => x.Number).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task PdfDecoder_WhenAzureReturnsOnlySomePages_ShouldNotIndexPartialText()
    {
        // Arrange
        var pdf = PdfDecoder(Content("", ""), out var client);
        client.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OcrPage> { new(1, "Only first page") });

        // Act
        var action = () => pdf.DecodeAsync(new BinaryData("%PDF-test"));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ImageDecoder_WhenAzureReturnsNoText_ShouldFailWithoutPlaceholder()
    {
        // Arrange
        var client = new Mock<IDocumentIntelligenceOcrClient>();
        client.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OcrPage> { new(1, "  ") });
        var decoder = new DocumentIntelligenceImageDecoder(client.Object, Options());

        // Act
        var action = () => decoder.DecodeAsync(BinaryData.FromBytes([1]));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ImageDecoder_WhenAzureFails_ShouldReturnSafeError()
    {
        // Arrange
        var client = new Mock<IDocumentIntelligenceOcrClient>();
        client.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("secret endpoint and key"));
        var decoder = new DocumentIntelligenceImageDecoder(client.Object, Options());

        // Act
        var action = () => decoder.DecodeAsync(BinaryData.FromBytes([1]));

        // Assert
        var error = await action.Should().ThrowAsync<InvalidOperationException>();
        error.Which.Message.Should().NotContain("secret endpoint and key");
    }

    [Fact]
    public async Task ImageDecoder_WhenCancelled_ShouldPropagateCancellation()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var client = new Mock<IDocumentIntelligenceOcrClient>();
        client.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(source.Token));
        var decoder = new DocumentIntelligenceImageDecoder(client.Object, Options());

        // Act
        var action = () => decoder.DecodeAsync(BinaryData.FromBytes([1]), source.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static DocumentIntelligenceOcrOptions Options(string tier = "F0") => new()
    {
        Enabled = true,
        Tier = tier,
        Endpoint = "https://example.cognitiveservices.azure.com",
        Auth = "ApiKey",
        ApiKey = "test-key"
    };

    private static byte[] Tiff(int pages, bool littleEndian)
    {
        var bytes = new byte[8 + pages * 6];
        bytes[0] = bytes[1] = littleEndian ? (byte)'I' : (byte)'M';
        if (littleEndian)
        {
            bytes[2] = 42;
            bytes[4] = 8;
        }
        else
        {
            bytes[3] = 42;
            bytes[7] = 8;
        }

        for (var index = 0; index < pages - 1; index++)
        {
            var next = 8 + (index + 1) * 6;
            var pointer = bytes.AsSpan(8 + index * 6 + 2, 4);
            if (littleEndian)
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(pointer, next);
            else
                System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(pointer, next);
        }

        return bytes;
    }

    private static byte[] Heif() =>
    [
        0, 0, 0, 16, (byte)'f', (byte)'t', (byte)'y', (byte)'p',
        (byte)'h', (byte)'e', (byte)'i', (byte)'f', 0, 0, 0, 0
    ];

    private static FileContent Content(params string[] pages)
    {
        var content = new FileContent("text/plain");
        for (var index = 0; index < pages.Length; index++)
            content.Sections.Add(new Chunk(pages[index], index + 1));
        return content;
    }

    private static DocumentIntelligencePdfDecoder PdfDecoder(
        FileContent native,
        out Mock<IDocumentIntelligenceOcrClient> client,
        string tier = "F0")
    {
        client = new Mock<IDocumentIntelligenceOcrClient>();
        var nativeDecoder = new Mock<IContentDecoder>();
        nativeDecoder.Setup(x => x.DecodeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(native);
        return new DocumentIntelligencePdfDecoder(client.Object, nativeDecoder.Object, Options(tier));
    }
}
