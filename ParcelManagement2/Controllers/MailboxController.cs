using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ParcelProject.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class MailboxController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MailboxController> _logger;

        public MailboxController(IConfiguration configuration, ILogger<MailboxController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public record AutoCollectRequest(string CondoId, string? Trigger = null, string? DeviceIp = null);
        public record AutoCollectResponse(bool Success, int Updated, List<string> PackIds, string? Message);

        // POST /api/mailbox/auto-collect
        [HttpPost("auto-collect")]
        public async Task<ActionResult<AutoCollectResponse>> AutoCollectLetters([FromBody] AutoCollectRequest req)
        {
            // --- 簡單 API Key 驗證（建議用 HTTPS） ---
            var apiKeyHeader = Request.Headers["X-Api-Key"].ToString();
            var expected = _configuration["ApiKeys:MailboxSecret"];
            if (string.IsNullOrWhiteSpace(expected) || apiKeyHeader != expected)
                return Unauthorized(new AutoCollectResponse(false, 0, new(), "Invalid API key"));

            if (string.IsNullOrWhiteSpace(req?.CondoId))
                return BadRequest(new AutoCollectResponse(false, 0, new(), "condoId is required"));

            try
            {
                var connStr = _configuration.GetConnectionString("DefaultConnection");
                using var cn = new SqlConnection(connStr);
                await cn.OpenAsync();

                using var tx = cn.BeginTransaction();

                // 先挑一位住戶作為 collector 與 red_id fallback
                string getResidentSql = @"
                    SELECT TOP 1 r.red_id, r.red_name
                    FROM Resident r
                    WHERE r.condo_id = @condoId
                    ORDER BY r.red_name;";

                string? fallbackRedId = null;
                string collectorName = "Mailbox Auto"; // 你可改：例如用住戶姓名或裝置名稱
                using (var cmd = new SqlCommand(getResidentSql, cn, tx))
                {
                    cmd.Parameters.AddWithValue("@condoId", req.CondoId);
                    using var rd = await cmd.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        fallbackRedId = rd["red_id"]?.ToString();
                        var rn = rd["red_name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(rn))
                            collectorName = rn; // 想要顯示住戶名當作領取人就用這行
                    }
                }

                // 更新條件：該戶、未取、未刪除、且類型為「信件」
                // 以 Mail.pack_name='信件' 來對應 Boxdetail.pack_type
                string updateSql = @"
                    ;WITH LetterTypes AS (
                        SELECT pack_type FROM Mail WHERE pack_name = N'信件'
                    ),
                    Targets AS (
                        SELECT b.pack_id
                        FROM Boxdetail b
                        WHERE b.condo_id = @condoId
                          AND ISNULL(b.deleted,0) = 0
                          AND b.status = 0
                          AND b.pack_type IN (SELECT pack_type FROM LetterTypes)
                    )
                    UPDATE b
                       SET b.status = 1,
                           b.pickup_datetime = GETDATE(),
                           b.collector_name = @collectorName,
                           b.red_id = CASE WHEN (b.red_id IS NULL OR LTRIM(RTRIM(b.red_id))='') 
                                           THEN ISNULL(@fallbackRedId, b.red_id) 
                                           ELSE b.red_id END
                    OUTPUT INSERTED.pack_id
                    FROM Boxdetail b
                    INNER JOIN Targets t ON t.pack_id = b.pack_id;";

                var updatedPackIds = new List<string>();
                using (var cmd = new SqlCommand(updateSql, cn, tx))
                {
                    cmd.Parameters.AddWithValue("@condoId", req.CondoId);
                    cmd.Parameters.AddWithValue("@collectorName", collectorName);
                    cmd.Parameters.AddWithValue("@fallbackRedId", (object?)fallbackRedId ?? DBNull.Value);

                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                        updatedPackIds.Add(rd.GetString(0));
                }

                await tx.CommitAsync();

                return Ok(new AutoCollectResponse(true, updatedPackIds.Count, updatedPackIds,
                    updatedPackIds.Count > 0 ? null : "No uncollected letters"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoCollectLetters failed for CondoId={CondoId}", req?.CondoId);
                return StatusCode(500, new AutoCollectResponse(false, 0, new(), ex.Message));
            }
        }
    }
}
