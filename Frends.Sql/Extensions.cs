using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
#pragma warning disable 1591

namespace Frends.Sql
{
    public static class Extensions
    {
        internal static IsolationLevel GetSqlTransactionIsolationLevel(this SqlTransactionIsolationLevel sqlTransactionIsolationLevel)
        {
            return GetEnum<IsolationLevel>(sqlTransactionIsolationLevel);
        }

        internal static CommandType GetSqlCommandType(this SqlCommandType sqlCommandType)
        {
            return GetEnum<CommandType>(sqlCommandType);
        }

        private static T GetEnum<T>(Enum enumValue)
        {
            return (T)Enum.Parse(typeof(T), enumValue.ToString());
        }

        //Get inserted row count with reflection
        //http://stackoverflow.com/a/12271001
        internal static int RowsCopiedCount(this SqlBulkCopy bulkCopy)
        {
            const string rowsCopiedFieldName = "_rowsCopied";
            FieldInfo rowsCopiedField = null;
            rowsCopiedField = typeof(SqlBulkCopy).GetField(rowsCopiedFieldName,
                BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            return rowsCopiedField != null ? (int)rowsCopiedField.GetValue(bulkCopy) : 0;
        }

        public static void SetEmptyDataRowsToNull(this DataSet dataSet)
        {
            foreach (var table in dataSet.Tables.Cast<DataTable>())
            {
                foreach (var row in table.Rows.Cast<DataRow>())
                {
                    foreach (var column in row.ItemArray)
                    {
                        if (column.ToString() == string.Empty)
                        {
                            var index = Array.IndexOf(row.ItemArray, column);
                            row[index] = null;
                        }
                    }
                }
            }
        }
        public static T GetFlag<T>(this bool value, T flag)
        {
            return value ? flag : default(T);
        }
    }
}
