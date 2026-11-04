/**
 * MyAIAgent | KernelMemory · Tests
 *
 * Verifies native Office and HTML extraction with guarded OCR fallback.
 */

using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using FluentAssertions;
using Microsoft.KernelMemory.DataFormats;
using Moq;
using MyAIAgent.KernelMemory.Infrastructure.Models;
using MyAIAgent.KernelMemory.Infrastructure.Services;

namespace MyAIAgent.KernelMemory.Tests.Unit.Infrastructure;

public sealed class DocumentIntelligenceOfficeDecoderTests
{
    private const string WordMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string ExcelMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PowerPointMime = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    [Theory]
    [InlineData(WordMime)]
    [InlineData(ExcelMime)]
    [InlineData(PowerPointMime)]
    [InlineData("text/html")]
    public void SupportsMimeType_WhenOfficeOrHtml_ShouldClaimOnlyItsMime(string mimeType)
    {
        // Arrange
        var decoder = Decoder(mimeType, Text("native"), out _);

        // Act
        var supportsConfigured = decoder.SupportsMimeType(mimeType);

        // Assert
        supportsConfigured.Should().BeTrue();
        decoder.SupportsMimeType("text/plain").Should().BeFalse();
    }

    [Theory]
    [InlineData(WordMime)]
    [InlineData(ExcelMime)]
    [InlineData(PowerPointMime)]
    [InlineData("text/html")]
    public async Task DecodeAsync_WhenNativeTextExists_ShouldKeepItWithoutAzure(string mimeType)
    {
        // Arrange
        var decoder = Decoder(mimeType, Text("Native searchable text"), out var azure);

        // Act
        var result = await decoder.DecodeAsync(new BinaryData("not examined when native text exists"));

        // Assert
        result.Sections.Single().Content.Should().Be("Native searchable text");
        azure.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DecodeAsync_WhenNativeDecoderThrows_ShouldNotCallAzure()
    {
        // Arrange
        var native = new Mock<IContentDecoder>();
        native.Setup(x => x.DecodeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("invalid document"));
        var azure = new Mock<IDocumentIntelligenceOcrClient>();
        var decoder = new DocumentIntelligenceOfficeDecoder(azure.Object, native.Object, Options(), WordMime);

        // Act
        var action = () => decoder.DecodeAsync(new BinaryData("invalid"));

        // Assert
        await action.Should().ThrowAsync<InvalidDataException>();
        azure.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(WordMime)]
    [InlineData("text/html")]
    public async Task DecodeAsync_WhenF0WordOrHtmlIsBlank_ShouldFailBeforeAzure(string mimeType)
    {
        // Arrange
        var decoder = Decoder(mimeType, Text(" "), out var azure);

        // Act
        var action = () => decoder.DecodeAsync(new BinaryData("<html></html>"));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*F0*");
        azure.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DecodeAsync_WhenS0HtmlIsBlank_ShouldUseAzurePageText()
    {
        // Arrange
        var decoder = Decoder("text/html", Text(""), out var azure, "S0");
        azure.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OcrPage(1, "Azure HTML text")]);

        // Act
        var result = await decoder.DecodeAsync(new BinaryData("<html><body>source</body></html>"));

        // Assert
        result.Sections.Select(x => (x.Number, x.Content)).Should().Equal((1, "Azure HTML text"));
        azure.Verify(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DecodeAsync_WhenS0WordIsBlank_ShouldUseAzurePageText()
    {
        // Arrange
        var decoder = Decoder(WordMime, Text(""), out var azure, "S0");
        azure.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OcrPage(1, "Azure Word text")]);

        // Act
        var result = await decoder.DecodeAsync(WordDocument("Source text"));

        // Assert
        result.Sections.Select(x => x.Content).Should().Equal("Azure Word text");
    }

    [Fact]
    public async Task DecodeAsync_WhenS0WordXmlExceedsSafetyBudget_ShouldFailBeforeAzure()
    {
        // Arrange
        var decoder = Decoder(WordMime, Text(""), out var azure, "S0");
        var document = WordDocument(new string('A', 6_000_001));

        // Act
        var action = () => decoder.DecodeAsync(document);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        azure.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DecodeAsync_WhenS0HtmlExceedsSafetyBudget_ShouldFailBeforeAzure()
    {
        // Arrange
        var decoder = Decoder("text/html", Text(""), out var azure, "S0");
        var html = new BinaryData(new string('A', 6_000_001));

        // Act
        var action = () => decoder.DecodeAsync(html);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        azure.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DecodeAsync_WhenS0WordPackageIsMalformed_ShouldFailBeforeAzure()
    {
        // Arrange
        var decoder = Decoder(WordMime, Text(""), out var azure, "S0");

        // Act
        var action = () => decoder.DecodeAsync(new BinaryData("not a DOCX package"));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        azure.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DecodeAsync_WhenF0WorkbookHasThreeBlankSheets_ShouldFailBeforeAzure()
    {
        // Arrange
        var decoder = Decoder(ExcelMime, Text(""), out var azure);
        var workbook = Workbook(3);

        // Act
        var action = () => decoder.DecodeAsync(workbook);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tier limit*");
        azure.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DecodeAsync_WhenWorkbookHasTwoBlankSheets_ShouldPreserveWorksheetNumbers()
    {
        // Arrange
        var decoder = Decoder(ExcelMime, Text(""), out var azure);
        azure.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OcrPage(1, "One"), new OcrPage(2, "Two")]);

        // Act
        var result = await decoder.DecodeAsync(Workbook(2));

        // Assert
        result.Sections.Select(x => x.Number).Should().Equal(1, 2);
    }

    [Fact]
    public async Task DecodeAsync_WhenF0PresentationHasThreeBlankSlides_ShouldFailBeforeAzure()
    {
        // Arrange
        var decoder = Decoder(PowerPointMime, Text(""), out var azure);
        var presentation = Presentation(3);

        // Act
        var action = () => decoder.DecodeAsync(presentation);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tier limit*");
        azure.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DecodeAsync_WhenHtmlHasDtd_ShouldFailBeforeAzure()
    {
        // Arrange
        var decoder = Decoder("text/html", Text(""), out var azure, "S0");

        // Act
        var action = () => decoder.DecodeAsync(new BinaryData("<!DOCTYPE html [<!ENTITY x 'expanded'>]><html>&x;</html>"));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        azure.VerifyNoOtherCalls();
    }

    private static DocumentIntelligenceOfficeDecoder Decoder(
        string mimeType, FileContent nativeContent, out Mock<IDocumentIntelligenceOcrClient> azure,
        string tier = "F0")
    {
        azure = new Mock<IDocumentIntelligenceOcrClient>();
        var native = new Mock<IContentDecoder>();
        native.Setup(x => x.DecodeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nativeContent);
        return new DocumentIntelligenceOfficeDecoder(azure.Object, native.Object, Options(tier), mimeType);
    }

    private static DocumentIntelligenceOcrOptions Options(string tier = "F0") => new()
    {
        Enabled = true,
        Tier = tier,
        Endpoint = "https://example.cognitiveservices.azure.com",
        Auth = "ApiKey",
        ApiKey = "test-key"
    };

    private static FileContent Text(string text)
    {
        var content = new FileContent("text/plain");
        content.Sections.Add(new Chunk(text, 1));
        return content;
    }

    private static BinaryData Workbook(int sheetCount)
    {
        using var workbook = new XLWorkbook();
        for (var number = 1; number <= sheetCount; number++)
            workbook.AddWorksheet($"Sheet{number}");
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return BinaryData.FromBytes(stream.ToArray());
    }

    private static BinaryData WordDocument(string text)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                new DocumentFormat.OpenXml.Wordprocessing.Body(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                            new DocumentFormat.OpenXml.Wordprocessing.Text(text)))));
            mainPart.Document.Save();
        }
        return BinaryData.FromBytes(stream.ToArray());
    }

    private static BinaryData Presentation(int slideCount)
    {
        using var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation, true))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new DocumentFormat.OpenXml.Presentation.Presentation(new SlideIdList());
            for (var number = 1; number <= slideCount; number++)
            {
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = new Slide(new CommonSlideData(new ShapeTree()));
                presentationPart.Presentation.SlideIdList!.Append(new SlideId
                {
                    Id = (uint)(255 + number),
                    RelationshipId = presentationPart.GetIdOfPart(slidePart)
                });
            }
            presentationPart.Presentation.Save();
        }
        return BinaryData.FromBytes(stream.ToArray());
    }
}
