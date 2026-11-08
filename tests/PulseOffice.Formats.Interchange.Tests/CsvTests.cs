using System.Globalization;
using System.Text;
using PulseOffice.Foundation.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests;

public class CsvTests
{
    private static readonly DelimitedText Delimited = new();

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook Leggi(string testo, CsvOptions? opzioni = null)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testo));
        var reader = new CsvSpreadsheetReader(
            Delimited, new CsvValueReader(), new CsvSniffer(Delimited),
            (opzioni ?? CsvOptions.Default) with { AutoDetect = opzioni is null });
        var risultato = reader.Read(stream);

        Assert.True(risultato.Succeeded);
        return risultato.Value!;
    }

    private static string Scrivi(Workbook workbook, CsvOptions? opzioni = null)
    {
        using var stream = new MemoryStream();
        new CsvSpreadsheetWriter(Delimited, opzioni).Write(workbook, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void IlRitornoACapoDentroLeVirgoletteNonSpezzaLaRiga()
    {
        var sheet = Leggi("a,\"prima\nseconda\",c\nx,y,z\n").Sheets[0];

        Assert.Equal("prima\nseconda", sheet[A("B1")].Value.ToString());
        Assert.Equal("x", sheet[A("A2")].Value.ToString());
    }

    [Fact]
    public void UnaVirgolettaInMezzoAlCampoNonInghiotteIlResto()
    {
        var sheet = Leggi("Tubo 3/4\",12\nVite,3\n").Sheets[0];

        Assert.Equal("Tubo 3/4\"", sheet[A("A1")].Value.ToString());
        Assert.Equal("Vite", sheet[A("A2")].Value.ToString());
    }

    [Fact]
    public void LeRigheELeColonneVuoteInTestaRestanoAlProprioPosto()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Foglio1").SetValue(A("B3"), PulseOffice.Sheets.Values.CellValue.Text("x"));

        Assert.Equal("x", Leggi(Scrivi(workbook)).Sheets[0][A("B3")].Value.ToString());
    }

    [Fact]
    public void LeVirgoletteRaddoppiateSonoUnaSola()
    {
        var sheet = Leggi("\"Detto \"\"Ada\"\"\",Torino\n").Sheets[0];

        Assert.Equal("Detto \"Ada\"", sheet[A("A1")].Value.ToString());
        Assert.Equal("Torino", sheet[A("B1")].Value.ToString());
    }

    [Fact]
    public void UnCampoVuotoNonSpostaLeColonne()
    {
        var sheet = Leggi("a,,c\n").Sheets[0];

        Assert.Equal("a", sheet[A("A1")].Value.ToString());
        Assert.True(sheet[A("B1")].Value.IsEmpty);
        Assert.Equal("c", sheet[A("C1")].Value.ToString());
    }

    [Fact]
    public void ILNumeriSeguonoLaLinguaScelta()
    {
        var italiano = new CsvOptions
        {
            Delimiter = DelimitedFormat.Semicolon,
            Culture = CultureInfo.GetCultureInfo("it-IT"),
        };

        var sheet = Leggi("1.250,50;3\n", italiano).Sheets[0];

        Assert.Equal(1250.5, sheet[A("A1")].Value.AsNumber());
        Assert.Equal(3d, sheet[A("B1")].Value.AsNumber());
    }

    [Fact]
    public void LoStessoTestoCambiaSignificatoConLaLingua()
    {
        var inglese = new CsvOptions { Delimiter = DelimitedFormat.Semicolon };
        var italiano = new CsvOptions
        {
            Delimiter = DelimitedFormat.Semicolon,
            Culture = CultureInfo.GetCultureInfo("it-IT"),
        };

        Assert.Equal(1250.5, Leggi("1,250.50\n", inglese).Sheets[0][A("A1")].Value.AsNumber());
        Assert.Equal("1,250.50", Leggi("1,250.50\n", italiano).Sheets[0][A("A1")].Value.ToString());
    }

    [Fact]
    public void Una_data_con_il_giorno_a_due_cifre_che_inizia_per_zero_resta_una_data()
    {
        var italiano = new CsvOptions { Delimiter = DelimitedFormat.Semicolon, Culture = CultureInfo.GetCultureInfo("it-IT") };

        var sheet = Leggi("25/10/2026;01/01/2026;05/03/2026 08:30;09:15\n", italiano).Sheets[0];

        Assert.Equal(46320d, sheet[A("A1")].Value.AsNumber());
        Assert.Equal(46023d, sheet[A("B1")].Value.AsNumber());
        Assert.True(sheet[A("C1")].Value.AsNumber() > 46000);
        Assert.Equal(9.25 / 24, sheet[A("D1")].Value.AsNumber(), 6);
    }

    [Fact]
    public void Un_CSV_salvato_da_Excel_in_Windows_1252_o_in_UTF_16_si_legge_con_le_lettere_accentate()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var reader = new CsvSpreadsheetReader(new PulseOffice.Foundation.Text.DelimitedText(), new CsvValueReader(), new CsvSniffer(new PulseOffice.Foundation.Text.DelimitedText()));
        foreach (var bytes in new[]
                 {
                     System.Text.Encoding.GetEncoding(1252).GetBytes("Città;Perché\r\nNapoli;Più €\r\n"),
                     [.. System.Text.Encoding.Unicode.GetPreamble(), .. System.Text.Encoding.Unicode.GetBytes("Città\tPerché\r\nNapoli\tPiù €\r\n")],
                 })
        {
            using var stream = new MemoryStream(bytes);
            Assert.True(reader.CanRead(stream));
            stream.Position = 0;
            var sheet = reader.Read(stream).Require().Sheets[0];
            Assert.Equal("Città", sheet[A("A1")].Value.ToString());
            Assert.Equal("Più €", sheet[A("B2")].Value.ToString());
        }
    }

    [Fact]
    public void Le_opzioni_scelte_valgono_con_piu_separatori_righe_saltate_e_tutto_come_testo()
    {
        var delimited = new PulseOffice.Foundation.Text.DelimitedText();
        var reader = new CsvSpreadsheetReader(delimited, new CsvValueReader(), new CsvSniffer(delimited));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Esportato il 25/10\nNome;Età|Peso\nAda;36|52,5\n"));
        var detected = reader.Detect(stream);

        var sheet = reader.WithOptions(detected with
        {
            Delimiter = new PulseOffice.Foundation.Text.DelimitedFormat { Separator = ';', OtherSeparators = "|" },
            SkipRows = 1,
            DetectTypes = false,
            Culture = CultureInfo.GetCultureInfo("it-IT"),
        }).Read(stream).Require().Sheets[0];

        Assert.Equal("Nome", sheet[A("A1")].Value.ToString());
        Assert.Equal("Peso", sheet[A("C1")].Value.ToString());
        Assert.Equal("52,5", sheet[A("C2")].Value.ToString());
    }

    [Fact]
    public void Il_separatore_si_indovina_anche_con_una_riga_di_intestazione_e_le_virgole_decimali() =>
        Assert.Equal(';', new PulseOffice.Foundation.Text.DelimitedText().GuessSeparator("Esportato da gestionale\nCittà;Importo;Data\nForlì;1.234,50;01/02/2026\nCantù;-0,75;15/03/2026\n"));

    [Fact]
    public void GliZeriInizialiRestanoTesto()
    {
        var sheet = Leggi("007,00123\n").Sheets[0];

        Assert.Equal("007", sheet[A("A1")].Value.ToString());
        Assert.Equal("00123", sheet[A("B1")].Value.ToString());
    }

    [Fact]
    public void IlSeparatoreSiIndovinaDalTesto()
    {
        Assert.Equal(';', Delimited.GuessSeparator("a;b;c\n1;2;3"));
        Assert.Equal(',', Delimited.GuessSeparator("a,b,c"));
        Assert.Equal('\t', Delimited.GuessSeparator("a\tb\tc"));
        Assert.Null(Delimited.GuessSeparator("una riga sola senza separatori"));
    }

    [Fact]
    public void IlSeparatoreDentroLeVirgoletteNonConta()
    {
        Assert.Equal(';', Delimited.GuessSeparator("\"Rossi, Ada\";Torino;3"));
    }

    [Fact]
    public void ScrivendoSiProteggonoVirgoletteESeparatori()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(A("A1"), CellValue.Text("Rossi, Ada"));
        sheet.SetValue(A("B1"), CellValue.Text("Detto \"Ada\""));
        sheet.SetValue(A("C1"), CellValue.Number(1250.5));

        var testo = Scrivi(workbook);

        Assert.Equal("\"Rossi, Ada\",\"Detto \"\"Ada\"\"\",1250.5\r\n", testo);
    }

    [Fact]
    public void IlGiroCompletoNonPerdeNulla()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(A("A1"), CellValue.Text("Zona"));
        sheet.SetValue(A("B1"), CellValue.Text("Totale"));
        sheet.SetValue(A("A2"), CellValue.Text("Nord"));
        sheet.SetValue(A("B2"), CellValue.Number(1250.5));

        var riletto = Leggi(Scrivi(workbook)).Sheets[0];

        Assert.Equal("Zona", riletto[A("A1")].Value.ToString());
        Assert.Equal("Nord", riletto[A("A2")].Value.ToString());
        Assert.Equal(1250.5, riletto[A("B2")].Value.AsNumber());
    }

    [Fact]
    public void UnFoglioVuotoProduceUnFileVuoto()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");

        Assert.Equal(string.Empty, Scrivi(workbook));
    }

    [Fact]
    public void LePerditeSonoDichiarate()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetFormula(A("A1"), "=1+1");
        workbook.AddSheet("Secondo");

        var rapporto = new CsvSpreadsheetWriter(Delimited).CheckFidelity(workbook);

        Assert.False(rapporto.IsPerfect);
        Assert.Contains(rapporto.Losses, loss => loss.Code == "CSV001");
        Assert.Contains(rapporto.Losses, loss => loss.Code == "CSV002");
    }

    [Fact]
    public void UnTestoCheSembraUnaFormulaVieneSegnalatoPrimaDellEsportazione()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(A("A1"), CellValue.Text("=HYPERLINK(\"http://example.invalid\";\"x\")"));

        var rapporto = new CsvSpreadsheetWriter(Delimited).CheckFidelity(workbook);

        Assert.Contains(rapporto.Losses, loss => loss.Code == "ODF259");
    }

    [Fact]
    public void UnTestoNormaleNonVieneSegnalatoComeFormula()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(A("A1"), CellValue.Text("uguale = a"));
        sheet.SetValue(A("A2"), CellValue.Number(-3));

        var rapporto = new CsvSpreadsheetWriter(Delimited).CheckFidelity(workbook);

        Assert.DoesNotContain(rapporto.Losses, loss => loss.Code == "ODF259");
    }

    [Fact]
    public void IlLettoreIndovinaSeparatoreELinguaDaSolo()
    {
        var testo = "Zona;Mese;Totale\nNord;1;1.250,50\n\"Rossi; Ada\";2;890,00\n";

        var sheet = Leggi(testo).Sheets[0];

        Assert.Equal("Zona", sheet[A("A1")].Value.ToString());
        Assert.Equal("Rossi; Ada", sheet[A("A3")].Value.ToString());
        Assert.Equal(1250.5, sheet[A("C2")].Value.AsNumber());
        Assert.Equal(890d, sheet[A("C3")].Value.AsNumber());
    }

    [Fact]
    public void UnFileIngleseNonDiventaItaliano()
    {
        var sheet = Leggi("Zone,Month,Total\nNorth,1,1250.50\nSouth,2,890.00\n").Sheets[0];

        Assert.Equal(1250.5, sheet[A("C2")].Value.AsNumber());
        Assert.Equal(890d, sheet[A("C3")].Value.AsNumber());
    }

    [Fact]
    public void UnPuntoInItalianoNonDiventaSeparatoreDelleMigliaia()
    {
        var sheet = Leggi("1.5;1.250,50;a\n", CsvOptions.Default with
        {
            Delimiter = DelimitedFormat.Semicolon,
            Culture = CultureInfo.GetCultureInfo("it-IT"),
        }).Sheets[0];

        Assert.Equal(ValueKind.Text, sheet[A("A1")].Value.Kind);
        Assert.Equal(1250.5, sheet[A("B1")].Value.AsNumber());
    }

    [Fact]
    public void UnaVirgolaSenzaGruppiDiTreNonDiventaUnNumero()
    {
        var sheet = Leggi("\"1,5\",\"1,234\",NaN,Infinity\n", CsvOptions.Default).Sheets[0];

        Assert.Equal(ValueKind.Text, sheet[A("A1")].Value.Kind);
        Assert.Equal(1234, sheet[A("B1")].Value.AsNumber());
        Assert.Equal(ValueKind.Text, sheet[A("C1")].Value.Kind);
        Assert.Equal(ValueKind.Text, sheet[A("D1")].Value.Kind);
    }

    [Fact]
    public void FrazioniEOrariNonDiventanoDateDiOggi()
    {
        var sheet = Leggi("3/4,12:30,1-2\n", CsvOptions.Default).Sheets[0];

        Assert.Equal(ValueKind.Text, sheet[A("A1")].Value.Kind);
        Assert.Equal(0.5208333, sheet[A("B1")].Value.AsNumber(), 6);
        Assert.Equal(ValueKind.Text, sheet[A("C1")].Value.Kind);
    }

    [Fact]
    public void UnaDataInUnaColonnaFormattataEsceComeData()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.Layout.SetColumnStyle(0, workbook.Styles.Intern(new PulseOffice.Sheets.Styling.CellStyle { NumberFormatId = 14 }));
        sheet[A("A1")] = new Cell { Value = CellValue.Number(45000) };

        Assert.Contains("2023", Scrivi(workbook, CsvOptions.Default with { Culture = System.Globalization.CultureInfo.InvariantCulture }));
    }

    [Fact]
    public void DateOreEPercentualiLetteDalCsvHannoIlLoroFormato()
    {
        var sheet = Leggi("2024-01-15;12:30:00;2024-01-15 08:00;12%\n", CsvOptions.Default with { Delimiter = DelimitedFormat.Semicolon, Culture = CultureInfo.InvariantCulture });
        var book = sheet;
        var foglio = book.Sheets[0];

        Assert.Equal(14, book.Styles.Get(foglio.StyleIdAt(A("A1"))).NumberFormatId);
        Assert.Equal(21, book.Styles.Get(foglio.StyleIdAt(A("B1"))).NumberFormatId);
        Assert.Equal(22, book.Styles.Get(foglio.StyleIdAt(A("C1"))).NumberFormatId);
        Assert.Equal(10, book.Styles.Get(foglio.StyleIdAt(A("D1"))).NumberFormatId);
        Assert.Equal(0.12, foglio[A("D1")].Value.AsNumber(), 10);
    }
}
