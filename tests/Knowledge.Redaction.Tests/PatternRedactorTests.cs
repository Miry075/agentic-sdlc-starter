using Knowledge.Redaction;

namespace Knowledge.Redaction.Tests;

public class PatternRedactorTests
{
    // All values below are synthetic: they pass the checksums but belong to nobody.
    private static readonly PatternRedactor Redactor = PatternRedactor.Create();

    [Theory]
    [InlineData("Contact jane.doe@example.test for access.", "Contact [EMAIL] for access.")]
    [InlineData("Pay BE71 0961 2345 6769 today.", "Pay [IBAN] today.")]
    [InlineData("Pay BE71096123456769 today.", "Pay [IBAN] today.")]
    [InlineData("Card 4111 1111 1111 1111 on file.", "Card [CARD] on file.")]
    [InlineData("Card 4111-1111-1111-1111 on file.", "Card [CARD] on file.")]
    [InlineData("RRN 85.07.30-033.28 recorded.", "RRN [NATIONAL_ID] recorded.")]
    [InlineData("RRN 05.02.15-123.60 recorded.", "RRN [NATIONAL_ID] recorded.")]
    [InlineData("Call +32 475 12 34 56 now.", "Call [PHONE] now.")]
    [InlineData("Call 0032 2 123 45 67 now.", "Call [PHONE] now.")]
    [InlineData("Call 02 123 45 67 now.", "Call [PHONE] now.")]
    [InlineData("Call 0475/12.34.56 now.", "Call [PHONE] now.")]
    [InlineData("Call 0475123456 now.", "Call [PHONE] now.")]
    public void Redact_WithValidSensitiveValue_ReplacesItWithItsPlaceholder(string input, string expected)
    {
        var result = Redactor.Redact(input);

        Assert.Equal(expected, result.Text);
        Assert.Equal(1, result.RedactionCount);
    }

    [Theory]
    [InlineData("Pay BE71 0961 2345 6760 today.")]       // IBAN checksum fails
    [InlineData("Card 4111 1111 1111 1112 on file.")]    // Luhn fails
    [InlineData("RRN 85.07.30-033.29 recorded.")]        // mod-97 fails
    public void Redact_WithInvalidChecksum_LeavesTheTextUnchanged(string input)
    {
        var result = Redactor.Redact(input);

        Assert.Equal(input, result.Text);
        Assert.Equal(0, result.RedactionCount);
    }

    [Fact]
    public void Redact_WithJwt_ReplacesIt()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ0ZXN0In0.c2lnbmF0dXJl";

        var result = Redactor.Redact($"Authorization: Bearer {jwt}");

        Assert.Equal("Authorization: Bearer [TOKEN]", result.Text);
    }

    [Theory]
    [InlineData("key sk-abcdefghijklmnopqrstuvwxyz123456 end")]
    [InlineData("key ghp_abcdefghijklmnopqrstuvwxyz1234 end")]
    [InlineData("key AKIAABCDEFGHIJKLMNOP end")]
    [InlineData("key xoxb-1234567890-abcdefghij end")]
    [InlineData("key password=hunter2 end")]
    [InlineData("key PWD=hunter2 end")]
    public void Redact_WithSecret_ReplacesIt(string input)
    {
        var result = Redactor.Redact(input);

        Assert.Equal("key [SECRET] end", result.Text);
    }

    [Fact]
    public void Redact_WithSqlConnectionString_ReplacesItAsAWhole()
    {
        const string input = "cs: Server=tcp:db.example.test,1433;Initial Catalog=orders;User ID=app;Password=hunter2;\nnext line";

        var result = Redactor.Redact(input);

        Assert.Equal("cs: [CONNECTION_STRING]\nnext line", result.Text);
        Assert.Equal(1, result.ByKind["CONNECTION_STRING"]);
        Assert.False(result.ByKind.ContainsKey("SECRET"));
    }

    [Fact]
    public void Redact_WithStorageAccountKey_ReplacesIt()
    {
        var result = Redactor.Redact("AccountName=demo;AccountKey=abc123==;EndpointSuffix=core.windows.net");

        Assert.Equal("[CONNECTION_STRING]", result.Text);
    }

    [Fact]
    public void Redact_WithSeveralKinds_CountsEachKind()
    {
        var result = Redactor.Redact("a@example.test b@example.test BE71096123456769");

        Assert.Equal(3, result.RedactionCount);
        Assert.Equal(2, result.ByKind["EMAIL"]);
        Assert.Equal(1, result.ByKind["IBAN"]);
    }

    [Fact]
    public void Redact_WithOrdinaryEngineeringText_LeavesItUnchanged()
    {
        const string text = "InvoiceService v2.3.1 calls PaymentGateway on 2026-10-15 (see ADR-0007, ticket #4521). Build 20261015.1.";

        var result = Redactor.Redact(text);

        Assert.Equal(text, result.Text);
        Assert.Equal(0, result.RedactionCount);
    }

    [Fact]
    public void Redact_WithEmptyText_ReturnsEmpty()
    {
        var result = Redactor.Redact(string.Empty);

        Assert.Equal(string.Empty, result.Text);
        Assert.Equal(0, result.RedactionCount);
    }

    [Fact]
    public void Redact_WhenRedactingAgain_DoesNotChangeTheResult()
    {
        var once = Redactor.Redact("Mail a@example.test, IBAN BE71096123456769, call +32 475 12 34 56.");

        var twice = Redactor.Redact(once.Text);

        Assert.Equal(once.Text, twice.Text);
        Assert.Equal(0, twice.RedactionCount);
    }

    [Fact]
    public void Create_WithCustomPattern_RedactsItsMatches()
    {
        var redactor = PatternRedactor.Create([new CustomRedactionPattern("customer_ref", @"\bCUST-\d{6}\b", "CUST-123456")]);

        var result = redactor.Redact("Order for CUST-654321 shipped.");

        Assert.Equal("Order for [CUSTOMER_REF] shipped.", result.Text);
    }

    [Fact]
    public void Create_WhenCustomPatternDoesNotMatchItsSample_Throws()
    {
        var pattern = new CustomRedactionPattern("customer_ref", @"\bCUST-\d{6}\b", "nothing to see");

        Assert.Throws<InvalidOperationException>(() => PatternRedactor.Create([pattern]));
    }
}
