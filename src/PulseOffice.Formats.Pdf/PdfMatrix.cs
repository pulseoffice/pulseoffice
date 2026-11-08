namespace PulseOffice.Formats.Pdf;

public readonly record struct PdfMatrix(double A, double B, double C, double D, double E, double F)
{
    public static PdfMatrix Identity { get; } = new(1, 0, 0, 1, 0, 0);

    public PdfMatrix Then(PdfMatrix outer) => new(
        A * outer.A + B * outer.C,
        A * outer.B + B * outer.D,
        C * outer.A + D * outer.C,
        C * outer.B + D * outer.D,
        E * outer.A + F * outer.C + outer.E,
        E * outer.B + F * outer.D + outer.F);

    public string Format(Func<double, string> number) =>
        $"{number(A)} {number(B)} {number(C)} {number(D)} {number(E)} {number(F)}";
}
