#if DEBUG
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Services;

namespace QLTTTA_API.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly IOracleConnectionProvider _userConnProvider;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            IWebHostEnvironment env,
            IConfiguration configuration,
            IOracleConnectionProvider userConnProvider,
            ILogger<HealthController> logger)
        {
            _env = env;
            _configuration = configuration;
            _userConnProvider = userConnProvider;
            _logger = logger;
        }

        [HttpGet("debug")]
        public async Task<IActionResult> DebugLocks(CancellationToken ct)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var headerSid = HttpContext.Request.Headers["X-Session-Id"].FirstOrDefault();
            int? userSid = null;
            string? userName = null;

            // 1) Lấy SID của session hiện tại qua kết nối của user (không cần quyền v$)
            try
            {
                using var userConn = await _userConnProvider.GetUserConnectionAsync(ct);
                using var sidCmd = new OracleCommand("SELECT SYS_CONTEXT('USERENV','SID') AS SID, SYS_CONTEXT('USERENV','SESSION_USER') AS USERNAME FROM DUAL", userConn);
                using var rdr = await sidCmd.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, ct);
                if (await rdr.ReadAsync(ct))
                {
                    var sidStr = rdr["SID"]?.ToString();
                    if (int.TryParse(sidStr, out var sidVal)) userSid = sidVal;
                    userName = rdr["USERNAME"]?.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể lấy SID từ kết nối user");
            }

            var adminCs = _configuration.GetConnectionString("OracleDbConnection");
            if (string.IsNullOrWhiteSpace(adminCs))
            {
                return Problem(detail: "Thiếu cấu hình OracleDbConnection", statusCode: 500);
            }

            var payload = new Dictionary<string, object?>
            {
                ["environment"] = _env.EnvironmentName,
                ["requestSessionId"] = headerSid,
                ["dbUser"] = userName,
                ["dbSid"] = userSid
            };

            // 2) Truy vấn thông tin session và lock qua kết nối admin (cần quyền v$)
            try
            {
                using var adminConn = new OracleConnection(adminCs);
                await adminConn.OpenAsync(ct);

                // v$session chi tiết
                var sessionRows = new List<Dictionary<string, object?>>();
                string sessionSql;
                if (userSid.HasValue)
                {
                    sessionSql = @"SELECT s.sid, s.serial#, s.username, s.status, s.machine, s.program, s.module, s.event, s.seconds_in_wait, s.blocking_session, s.final_blocking_session, TO_CHAR(s.logon_time,'YYYY-MM-DD HH24:MI:SS') AS logon_time
                                     FROM v$session s
                                    WHERE s.sid = :sid";
                }
                else
                {
                    sessionSql = @"SELECT s.sid, s.serial#, s.username, s.status, s.machine, s.program, s.module, s.event, s.seconds_in_wait, s.blocking_session, s.final_blocking_session, TO_CHAR(s.logon_time,'YYYY-MM-DD HH24:MI:SS') AS logon_time
                                     FROM v$session s
                                    WHERE s.username = :user
                                 ORDER BY s.logon_time DESC FETCH FIRST 5 ROWS ONLY";
                }

                using (var sCmd = new OracleCommand(sessionSql, adminConn) { BindByName = true })
                {
                    if (userSid.HasValue)
                        sCmd.Parameters.Add(":sid", OracleDbType.Int32).Value = userSid.Value;
                    else
                        sCmd.Parameters.Add(":user", OracleDbType.Varchar2).Value = (userName ?? string.Empty).ToUpperInvariant();

                    using var sr = await sCmd.ExecuteReaderAsync(ct);
                    while (await sr.ReadAsync(ct))
                    {
                        sessionRows.Add(new Dictionary<string, object?>
                        {
                            ["sid"] = sr["sid"],
                            ["serial#"] = sr["serial#"],
                            ["username"] = sr["username"],
                            ["status"] = sr["status"],
                            ["machine"] = sr["machine"],
                            ["program"] = sr["program"],
                            ["module"] = sr["module"],
                            ["event"] = sr["event"],
                            ["seconds_in_wait"] = sr["seconds_in_wait"],
                            ["blocking_session"] = sr["blocking_session"],
                            ["final_blocking_session"] = sr["final_blocking_session"],
                            ["logon_time"] = sr["logon_time"]
                        });
                    }
                }
                payload["sessions"] = sessionRows;

                // locks của session
                var lockRows = new List<Dictionary<string, object?>>();
                string locksSql = @"SELECT lo.session_id AS sid,
                                            s.serial#,
                                            o.owner,
                                            o.object_name,
                                            o.object_type,
                                            lo.locked_mode
                                       FROM v$locked_object lo
                                       JOIN dba_objects o ON o.object_id = lo.object_id
                                       JOIN v$session s ON s.sid = lo.session_id
                                      WHERE " + (userSid.HasValue ? "lo.session_id = :sid" : "s.username = :user") + @"
                                   ORDER BY lo.locked_mode DESC, o.object_name";

                using (var lCmd = new OracleCommand(locksSql, adminConn) { BindByName = true })
                {
                    if (userSid.HasValue)
                        lCmd.Parameters.Add(":sid", OracleDbType.Int32).Value = userSid.Value;
                    else
                        lCmd.Parameters.Add(":user", OracleDbType.Varchar2).Value = (userName ?? string.Empty).ToUpperInvariant();

                    using var lr = await lCmd.ExecuteReaderAsync(ct);
                    while (await lr.ReadAsync(ct))
                    {
                        lockRows.Add(new Dictionary<string, object?>
                        {
                            ["sid"] = lr["sid"],
                            ["serial#"] = lr["serial#"],
                            ["owner"] = lr["owner"],
                            ["object_name"] = lr["object_name"],
                            ["object_type"] = lr["object_type"],
                            ["locked_mode"] = lr["locked_mode"]
                        });
                    }
                }
                payload["locks"] = lockRows;

                // quan hệ blocker/waiter nếu có
                var waits = new List<Dictionary<string, object?>>();
                string waitSql = @"SELECT bw.blocker_sid, bw.requester_sid AS waiter_sid
                                     FROM (
                                           SELECT l1.sid AS blocker_sid, l2.sid AS requester_sid
                                             FROM v$lock l1
                                             JOIN v$lock l2 ON l1.id1 = l2.id1 AND l1.id2 = l2.id2
                                            WHERE l1.block = 1 AND l2.request > 0
                                          ) bw
                                    WHERE " + (userSid.HasValue ? "bw.blocker_sid = :sid OR bw.requester_sid = :sid" : "bw.blocker_sid IN (SELECT sid FROM v$session WHERE username = :user) OR bw.requester_sid IN (SELECT sid FROM v$session WHERE username = :user)");

                using (var wCmd = new OracleCommand(waitSql, adminConn) { BindByName = true })
                {
                    if (userSid.HasValue)
                        wCmd.Parameters.Add(":sid", OracleDbType.Int32).Value = userSid.Value;
                    else
                        wCmd.Parameters.Add(":user", OracleDbType.Varchar2).Value = (userName ?? string.Empty).ToUpperInvariant();

                    using var wr = await wCmd.ExecuteReaderAsync(ct);
                    while (await wr.ReadAsync(ct))
                    {
                        waits.Add(new Dictionary<string, object?>
                        {
                            ["blocker_sid"] = wr["blocker_sid"],
                            ["waiter_sid"] = wr["waiter_sid"]
                        });
                    }
                }
                payload["block_wait"] = waits;
            }
            catch (OracleException oex)
            {
                // Thiếu quyền trên v$ hay dba_*
                payload["error"] = $"Oracle error {oex.Number}: {oex.Message}";
            }
            catch (Exception ex)
            {
                payload["error"] = ex.Message;
            }

            return Ok(payload);
        }
    }
}
#endif
