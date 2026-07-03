using System.Data;
using System.Runtime.CompilerServices;
using Dapper;

namespace TripPlanner.Database.Connections;

internal static class DapperTypeHandlers
{
    [ModuleInitializer]
    internal static void Register()
    {
        SqlMapper.AddTypeHandler(new DateOnlyHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(new NullableDateTimeOffsetHandler());
    }

    private sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }

        public override DateOnly Parse(object value) => value switch
        {
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            string s => DateOnly.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new DataException($"Cannot convert {value.GetType()} to DateOnly")
        };
    }

    private sealed class NullableDateOnlyHandler : SqlMapper.TypeHandler<DateOnly?>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly? value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
        }

        public override DateOnly? Parse(object value) => value switch
        {
            null => null,
            DBNull => null,
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            string s => DateOnly.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new DataException($"Cannot convert {value.GetType()} to DateOnly?")
        };
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.Value = value.UtcDateTime;
        }

        public override DateTimeOffset Parse(object value) => value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
            string s => DateTimeOffset.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new DataException($"Cannot convert {value.GetType()} to DateTimeOffset")
        };
    }

    private sealed class NullableDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset?>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset? value)
        {
            parameter.Value = value.HasValue ? value.Value.UtcDateTime : DBNull.Value;
        }

        public override DateTimeOffset? Parse(object value) => value switch
        {
            null => null,
            DBNull => null,
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
            string s => DateTimeOffset.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new DataException($"Cannot convert {value.GetType()} to DateTimeOffset?")
        };
    }
}
