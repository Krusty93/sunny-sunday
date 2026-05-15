using System.Data;
using Dapper;

namespace Relego.Server.Data;

public sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.Value = value.UtcDateTime.ToString("O");
    }

    public override DateTimeOffset Parse(object value)
    {
        return DateTimeOffset.Parse((string)value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }
}
