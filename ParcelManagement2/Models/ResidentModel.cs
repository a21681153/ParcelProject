using System.Data;

namespace ParcelManagement2.Models
{
    public class ResidentModel
    {
        private readonly DBConn _dbConn;
        public ResidentModel(DBConn dbConn) 
        {
            _dbConn = dbConn;
        }

        /* 依戶號取全部住戶 */
        public DataTable ListByCondo(string condoId)
        {
            const string sql = "SELECT red_id, red_name, phone FROM dbo.Resident WHERE condo_id = @condoId";
            var parameters = new Dictionary<string, object>
            {
                { "@condoId", condoId }
            };
            return _dbConn.GetDataTable(sql, parameters);
        }

        /* 第一筆住戶 (通常即本人) */
        public DataRow? GetRowByCondoFirst(string condoId)
        {
            const string sql = "SELECT TOP 1 red_id, condo_id, red_name, phone FROM dbo.Resident WHERE condo_id = @condoId";
            var parameters = new Dictionary<string, object>
            {
                { "@condoId", condoId }
            };
            DataTable dt = _dbConn.GetDataTable(sql,parameters);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        /* 單筆住戶 */
        public DataRow? GetRowById(string redId)
        {
            const string sql = "SELECT red_id, condo_id, red_name, phone FROM dbo.Resident WHERE red_id = @redId";
            var parameters = new Dictionary<string, object>
            {
                { "@redId", redId }
            };
            DataTable dt = _dbConn.GetDataTable(sql, parameters);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        /* 更新姓名與電話 */
        public string Update(string redId, string name, string phone)
        {
            const string sql = "UPDATE dbo.Resident SET red_name = @name, phone    = @phone WHERE red_id = @redId";
            var parameters = new Dictionary<string, object>
            {
                { "@name", name },
                { "@phone", phone },
                { "@redId", redId }
            };
            return _dbConn.ExecSQL(sql,parameters);
        }
    }
}
