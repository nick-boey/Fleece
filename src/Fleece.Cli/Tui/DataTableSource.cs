using Terminal.Gui;

namespace Fleece.Cli.Tui;

/// <summary>
/// DataTable source adapter for Terminal.Gui TableView.
/// </summary>
public sealed class DataTableSource : ITableSource
{
    private readonly System.Data.DataTable _table;

    public DataTableSource(System.Data.DataTable table)
    {
        _table = table;
    }

    public int Rows => _table.Rows.Count;
    public int Columns => _table.Columns.Count;

    public string[] ColumnNames => _table.Columns.Cast<System.Data.DataColumn>()
        .Select(c => c.ColumnName)
        .ToArray();

    public object this[int row, int col] => _table.Rows[row][col];
}
