namespace Knowledge.Redaction;

internal static class Checksums
{
    /// <summary>ISO 13616 mod-97 check on an IBAN written with or without spaces.</summary>
    public static bool IsValidIban(string value)
    {
        var iban = new string(value.Where(c => c != ' ').ToArray()).ToUpperInvariant();
        if (iban.Length is < 15 or > 34)
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            if (char.IsDigit(c))
            {
                remainder = (remainder * 10 + (c - '0')) % 97;
            }
            else if (c is >= 'A' and <= 'Z')
            {
                remainder = (remainder * 100 + (c - 'A' + 10)) % 97;
            }
            else
            {
                return false;
            }
        }

        return remainder == 1;
    }

    /// <summary>Luhn check on a card number written with optional spaces or hyphens. 13 to 19 digits.</summary>
    public static bool IsValidCardNumber(string value)
    {
        var digits = value.Where(char.IsDigit).Select(c => c - '0').ToArray();
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i];
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }

    /// <summary>
    /// Belgian national register number "YY.MM.DD-XXX.CC". The check is 97 minus the first nine digits
    /// modulo 97; for people born from 2000 the nine digits are prefixed with 2.
    /// </summary>
    public static bool IsValidBelgianNationalNumber(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 11)
        {
            return false;
        }

        var body = long.Parse(digits[..9], System.Globalization.CultureInfo.InvariantCulture);
        var check = int.Parse(digits[9..], System.Globalization.CultureInfo.InvariantCulture);

        return check == 97 - (int)(body % 97)
            || check == 97 - (int)((2_000_000_000L + body) % 97);
    }
}
