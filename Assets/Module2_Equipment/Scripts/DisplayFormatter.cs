using System;

namespace VRScienceLab.Module2
{
    /// <summary>
    /// Allocation-free numeric formatting into a reusable char buffer.
    /// Instruments write their readings through this class and push the buffer to
    /// TextMeshPro with <c>TMP_Text.SetCharArray</c>, so refreshing a display every
    /// frame does not allocate a new string.
    /// </summary>
    public static class DisplayFormatter
    {
        /// <summary>Buffer size large enough for any value produced by this formatter.</summary>
        public const int BufferSize = 48;

        /// <summary>Maximum number of fraction digits supported.</summary>
        public const int MaxDecimals = 4;

        private static readonly long[] PowersOfTen = { 1, 10, 100, 1000, 10000 };

        /// <summary>
        /// Returns <paramref name="value"/> scaled to an integer with <paramref name="decimals"/>
        /// fraction digits, rounded away from zero. Two values with the same scaled result display
        /// identically, so callers use it to skip redundant display updates.
        /// </summary>
        public static long ScaledValue(float value, int decimals)
        {
            decimals = Clamp(decimals, 0, MaxDecimals);
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0;
            }

            double scaled = Math.Round((double)value * PowersOfTen[decimals], MidpointRounding.AwayFromZero);
            if (scaled > long.MaxValue / 2) return long.MaxValue / 2;
            if (scaled < long.MinValue / 2) return long.MinValue / 2;
            return (long)scaled;
        }

        /// <summary>
        /// Writes <paramref name="value"/> with a fixed number of fraction digits followed by
        /// <paramref name="suffix"/> (for example "250.0 g"). Returns the number of characters written.
        /// NaN or infinity is written as "--" followed by the suffix.
        /// </summary>
        public static int FormatFixed(char[] buffer, float value, int decimals, string suffix)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            decimals = Clamp(decimals, 0, MaxDecimals);

            int length = 0;
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                length = Append(buffer, length, "--");
                return Append(buffer, length, suffix);
            }

            long scaled = ScaledValue(value, decimals);
            if (scaled < 0)
            {
                buffer[length++] = '-';
                scaled = -scaled;
            }

            long divisor = PowersOfTen[decimals];
            long integerPart = scaled / divisor;
            long fractionPart = scaled % divisor;

            length = AppendInteger(buffer, length, integerPart, 1);
            if (decimals > 0)
            {
                buffer[length++] = '.';
                length = AppendInteger(buffer, length, fractionPart, decimals);
            }

            return Append(buffer, length, suffix);
        }

        /// <summary>
        /// Writes a stopwatch time as mm:ss.ff (minutes, seconds, hundredths of a second).
        /// Minutes grow beyond two digits when needed. Returns the number of characters written.
        /// </summary>
        public static int FormatTime(char[] buffer, double totalSeconds)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (double.IsNaN(totalSeconds) || totalSeconds < 0) totalSeconds = 0;

            long totalHundredths = (long)Math.Floor(totalSeconds * 100.0);
            long minutes = totalHundredths / 6000;
            long seconds = (totalHundredths / 100) % 60;
            long hundredths = totalHundredths % 100;

            int length = AppendInteger(buffer, 0, minutes, 2);
            buffer[length++] = ':';
            length = AppendInteger(buffer, length, seconds, 2);
            buffer[length++] = '.';
            length = AppendInteger(buffer, length, hundredths, 2);
            return length;
        }

        /// <summary>Convenience for tests and logging: formats into a new string (allocates).</summary>
        public static string FormatFixedToString(float value, int decimals, string suffix)
        {
            var buffer = new char[BufferSize];
            int length = FormatFixed(buffer, value, decimals, suffix);
            return new string(buffer, 0, length);
        }

        /// <summary>Convenience for tests and logging: formats into a new string (allocates).</summary>
        public static string FormatTimeToString(double totalSeconds)
        {
            var buffer = new char[BufferSize];
            int length = FormatTime(buffer, totalSeconds);
            return new string(buffer, 0, length);
        }

        private static int AppendInteger(char[] buffer, int start, long value, int minDigits)
        {
            // Count digits first so the number can be written left to right without reversing.
            int digits = 1;
            for (long v = value; v >= 10; v /= 10) digits++;
            if (digits < minDigits) digits = minDigits;

            int end = start + digits;
            for (int i = end - 1; i >= start; i--)
            {
                buffer[i] = (char)('0' + (value % 10));
                value /= 10;
            }

            return end;
        }

        private static int Append(char[] buffer, int start, string text)
        {
            if (string.IsNullOrEmpty(text)) return start;
            for (int i = 0; i < text.Length && start < buffer.Length; i++)
            {
                buffer[start++] = text[i];
            }

            return start;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
