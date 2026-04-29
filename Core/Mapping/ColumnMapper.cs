using Syncro.Core.Models.Enums;

namespace Syncro.Core.Mapping;

public static class ColumnMapper
{
    public static Column MapColumnStringToEnum(this string column)
    {
        return column switch
        {
            "To do" => Column.Todo,
            "In progress" => Column.InProgress,
            "Review" => Column.Review,
            "Done" => Column.Done,
            _ => Column.Backlog
        };
    }
    
    public static string MapColumnEnumToString(this Column column)
    {
        return column switch
        {
            Column.Todo => "To do",
            Column.InProgress => "In progress",
            Column.Review => "Review",
            Column.Done => "Done",
            _ => "Column.Backlog"
        };
    }
}