using System;
using System.Collections.Generic;

public static class Base32Utility
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] FromBase32String(string base32)
    {
        if (string.IsNullOrEmpty(base32)) return new byte[0];
        base32 = base32.TrimEnd('=').ToUpperInvariant();

        List<byte> output = new List<byte>();
        int buffer = 0, bitsLeft = 0;

        foreach (char c in base32)
        {
            int val = Alphabet.IndexOf(c);
            if (val < 0) throw new FormatException("Invalid Base32 character.");

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }
        return output.ToArray();
    }
}