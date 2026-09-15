using System.Globalization;
using System.Text;

namespace Modernization.ChannelApi.Exports;

/// <summary>
/// Una celda de CSV ya escapada.
/// </summary>
/// <remarks>
/// <b>Inyeccion de formulas (CSV injection, OWASP).</b> Un texto que empieza con
/// <c>=</c>, <c>+</c>, <c>-</c> o <c>@</c> —aun detras de espacios—, o con tabulador o
/// retorno, se ejecuta como formula al abrir el archivo en una planilla. Los textos se
/// neutralizan con un apostrofo delante. Los numeros los formatea este codigo y no se
/// tocan, para que un -1,5 siga siendo un numero.
/// </remarks>
public readonly record struct CsvCell
{
    private CsvCell(string value) => Value = value;

    public string Value { get; }

    public static CsvCell Text(string? value) => new(Quote(Neutralize(value)));

    public static CsvCell Number(decimal? value)
        => new(value?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty);

    public static CsvCell Number(double? value)
        => new(value?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty);

    public static CsvCell Number(int? value)
        => new(value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    public static CsvCell YesNo(bool? value) => new(value switch
    {
        true => "Yes",
        false => "No",
        null => string.Empty,
    });

    private static string Neutralize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 1);

        foreach (var character in value)
        {
            // Un salto de linea dentro de una celda es legal en CSV, pero ningun dato de
            // estos lo necesita y parte la fila en mas de un lector.
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        var clean = builder.ToString();
        var first = clean.TrimStart();
        var dangerous = value[0] is '\t' or '\r'
            || (first.Length > 0 && "=+-@".Contains(first[0], StringComparison.Ordinal));

        return dangerous ? "'" + clean : clean;
    }

    private static string Quote(string value)
        => value.AsSpan().IndexOfAny(",\"") >= 0 || value.StartsWith(' ') || value.EndsWith(' ')
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
}

/// <summary>
/// Un CSV en memoria, con BOM UTF-8 para que las planillas lean bien los acentos.
/// </summary>
public sealed class CsvDocument
{
    /// <summary>Tope de filas: exportar no es una via para vaciar la fuente.</summary>
    public const int MaxRows = 50_000;

    private readonly StringBuilder _text = new();
    private readonly int _columns;

    public CsvDocument(params string[] headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        _columns = headers.Length;
        Append([.. headers.Select(CsvCell.Text)]);
    }

    /// <summary>Filas de datos, sin contar el encabezado.</summary>
    public int RowCount { get; private set; }

    public void AddRow(params CsvCell[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        if (cells.Length != _columns)
        {
            throw new ArgumentException("La fila no tiene la misma cantidad de columnas que el encabezado.", nameof(cells));
        }

        if (RowCount >= MaxRows)
        {
            throw new InvalidOperationException("La exportacion supera el tope de filas.");
        }

        Append(cells);
        RowCount++;
    }

    public byte[] ToBytes() => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(_text.ToString())];

    private void Append(CsvCell[] cells)
    {
        for (var index = 0; index < cells.Length; index++)
        {
            if (index > 0)
            {
                _text.Append(',');
            }

            _text.Append(cells[index].Value);
        }

        _text.Append("\r\n");
    }
}
