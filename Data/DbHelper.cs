using System.Data;
using Microsoft.Data.SqlClient;

namespace BizSuite.Data
{
    public class DbHelper
    {
        private readonly string _connectionString;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DbHelper(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _connectionString = config.GetConnectionString("BizSuiteDb")
                ?? throw new InvalidOperationException("Connection string 'BizSuiteDb' not found.");
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetConnectionString() => _connectionString;

        public SqlConnection GetConnection() => new SqlConnection(_connectionString);

        private void SetSessionContext(SqlConnection conn)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var companyIdClaim = user.FindFirst("CompanyId");
                if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int companyId))
                {
                    using var cmd = new SqlCommand("sp_set_session_context", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@key", "CompanyId");
                    cmd.Parameters.AddWithValue("@value", companyId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public DataTable ExecuteQuery(string sqlOrSp, params SqlParameter[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(sqlOrSp, conn);
            cmd.CommandType = sqlOrSp.Trim().Contains(" ") ? CommandType.Text : CommandType.StoredProcedure;
            cmd.Parameters.AddRange(parameters);
            conn.Open();
            SetSessionContext(conn);
            var dt = new DataTable();
            dt.Load(cmd.ExecuteReader());
            return dt;
        }

        public DataSet ExecuteDataSet(string sqlOrSp, params SqlParameter[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(sqlOrSp, conn);
            cmd.CommandType = sqlOrSp.Trim().Contains(" ") ? CommandType.Text : CommandType.StoredProcedure;
            cmd.Parameters.AddRange(parameters);
            conn.Open();
            SetSessionContext(conn);
            var ds = new DataSet();
            using var da = new SqlDataAdapter(cmd);
            da.Fill(ds);
            return ds;
        }

        public int ExecuteNonQuery(string sqlOrSp, params SqlParameter[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(sqlOrSp, conn);
            cmd.CommandType = sqlOrSp.Trim().Contains(" ") ? CommandType.Text : CommandType.StoredProcedure;
            cmd.Parameters.AddRange(parameters);
            conn.Open();
            SetSessionContext(conn);
            return cmd.ExecuteNonQuery();
        }

        public object? ExecuteScalar(string sqlOrSp, params SqlParameter[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(sqlOrSp, conn);
            cmd.CommandType = sqlOrSp.Trim().Contains(" ") ? CommandType.Text : CommandType.StoredProcedure;
            cmd.Parameters.AddRange(parameters);
            conn.Open();
            SetSessionContext(conn);
            return cmd.ExecuteScalar();
        }

        public DataTable ExecuteWithTVP(string spName, string tvpParamName, DataTable tvpData, params SqlParameter[] extraParams)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(spName, conn) { CommandType = CommandType.StoredProcedure };

            var tvpParam = new SqlParameter(tvpParamName, tvpData)
            {
                TypeName  = "dbo.OrderItemType",
                SqlDbType = SqlDbType.Structured
            };
            cmd.Parameters.Add(tvpParam);
            cmd.Parameters.AddRange(extraParams);
            conn.Open();
            SetSessionContext(conn);
            var dt = new DataTable();
            dt.Load(cmd.ExecuteReader());
            return dt;
        }

        public void LogAudit(int companyId, string tableName, string actionType, string details)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            int? userId = null;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var idClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim != null && int.TryParse(idClaim.Value, out int id)) userId = id;
            }

            var ip = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            const string sql = "INSERT INTO AuditLogs (CompanyId, UserId, TableName, ActionType, ActionDate, Details, IPAddress) VALUES (@C, @U, @T, @A, GETDATE(), @D, @IP)";
            ExecuteNonQuery(sql,
                P("@C", companyId),
                P("@U", userId),
                P("@T", tableName),
                P("@A", actionType),
                P("@D", details),
                P("@IP", ip));
        }

        public void AddNotification(int companyId, int? userId, string title, string message, string type = "info")
        {
            const string sql = "INSERT INTO Notifications (CompanyId, UserId, Title, Message, Type, IsRead, CreatedDate) VALUES (@C, @U, @T, @M, @Type, 0, GETDATE())";
            ExecuteNonQuery(sql,
                P("@C", companyId),
                P("@U", userId),
                P("@T", title),
                P("@M", message),
                P("@Type", type));
        }

        public static SqlParameter P(string name, object? value, SqlDbType? type = null)
        {
            var p = new SqlParameter(name, value ?? DBNull.Value);
            if (type.HasValue) p.SqlDbType = type.Value;
            return p;
        }
    }
}