namespace PulseOffice.UI.Controls.StylePanels;

public sealed class NaturalTitleComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                var startX = i;
                var startY = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                var numberX = x[startX..i].TrimStart('0');
                var numberY = y[startY..j].TrimStart('0');
                var byLength = numberX.Length.CompareTo(numberY.Length);
                if (byLength != 0) return byLength;
                var byDigits = string.CompareOrdinal(numberX, numberY);
                if (byDigits != 0) return byDigits;
                continue;
            }
            var byText = string.Compare(x[i].ToString(), y[j].ToString(), StringComparison.CurrentCultureIgnoreCase);
            if (byText != 0) return byText;
            i++;
            j++;
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }
}
