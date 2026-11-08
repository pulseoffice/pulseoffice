using System.Text;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public sealed class ThaiBahtText
{
    private readonly string[] _digits = ["ศูนย์", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า"];
    private readonly string[] _positions = ["", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน"];

    public string Spell(double value)
    {
        var negative = value < 0;
        var cents = (long)Math.Round(Math.Abs(value) * 100, MidpointRounding.AwayFromZero);
        var baht = cents / 100;
        var satang = cents % 100;

        var text = new StringBuilder(negative ? "ลบ" : string.Empty);
        if (baht == 0 && satang == 0) return text.Append(_digits[0]).Append("บาทถ้วน").ToString();
        if (baht > 0) text.Append(Number(baht)).Append("บาท");
        return satang == 0 ? text.Append("ถ้วน").ToString() : text.Append(Number(satang)).Append("สตางค์").ToString();
    }

    private string Number(long value)
    {
        var millions = value / 1_000_000;
        var rest = value % 1_000_000;
        var text = new StringBuilder();
        if (millions > 0) text.Append(Number(millions)).Append("ล้าน");
        if (rest > 0) text.Append(Block(rest, standsAlone: millions == 0));
        return text.ToString();
    }

    private string Block(long value, bool standsAlone)
    {
        var text = new StringBuilder();
        var digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var index = 0; index < digits.Length; index++)
        {
            var digit = digits[index] - '0';
            var position = digits.Length - 1 - index;
            if (digit == 0) continue;
            text.Append(position switch
            {
                1 when digit == 1 => string.Empty,
                1 when digit == 2 => "ยี่",
                0 when digit == 1 && (value > 9 || !standsAlone) => "เอ็ด",
                _ => _digits[digit],
            });
            text.Append(_positions[position]);
        }
        return text.ToString();
    }
}
