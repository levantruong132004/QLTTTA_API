using Oracle.ManagedDataAccess.Client;
using QLTTTA_API.Models;
using QLTTTA_API.Models.DTOs;

namespace QLTTTA_API.Services
{
    public interface IClassService
    {
        Task<List<Class>> GetClassesAsync(int? courseId = null, string? search = null);
        Task<Class?> GetClassByIdAsync(int id);
        Task<ApiResponse<Class>> CreateClassAsync(ClassCreateDto dto);
        Task<ApiResponse<Class>> UpdateClassAsync(ClassUpdateDto dto);
        Task<ApiResponse<bool>> DeleteClassAsync(int id);
        Task<List<Student>> GetRosterAsync(int classId);
    }

    public class ClassService : BaseService, IClassService
    {
        public ClassService(IConfiguration configuration, ILogger<ClassService> logger, IOracleConnectionProvider userConnProvider, IHttpContextAccessor httpContextAccessor)
            : base(configuration, logger, userConnProvider, httpContextAccessor) { }

        public async Task<List<Class>> GetClassesAsync(int? courseId = null, string? search = null)
        {
            var where = new List<string>();
            if (courseId.HasValue) where.Add("ID_KHOA_HOC = :courseid");
            if (!string.IsNullOrWhiteSpace(search)) where.Add("(UPPER(TEN_LOP_HOC) LIKE UPPER(:s) OR UPPER(MA_LOP_HOC) LIKE UPPER(:s))");
            var whereSql = where.Count > 0 ? (" WHERE " + string.Join(" AND ", where)) : string.Empty;
            var sql = $@"SELECT 
                                     ID_LOP_HOC,
                                     MA_LOP_HOC,
                                     TEN_LOP_HOC,
                                     NGAY_BAT_DAU,
                                     NGAY_KET_THUC,
                                     SI_SO_TOI_DA,
                                     ID_KHOA_HOC,
                                     ID_GIANG_VIEN,
                                     TRANG_THAI
                                 FROM LOP_HOC{whereSql} ORDER BY ID_LOP_HOC";
            object? parameters = null;
            if (courseId.HasValue && !string.IsNullOrWhiteSpace(search)) parameters = new { courseid = courseId.Value, s = $"%{search}%" };
            else if (courseId.HasValue) parameters = new { courseid = courseId.Value };
            else if (!string.IsNullOrWhiteSpace(search)) parameters = new { s = $"%{search}%" };
            return await ExecuteQueryAsync<Class>(sql, parameters);
        }

        public async Task<Class?> GetClassByIdAsync(int id)
        {
            var sql = @"SELECT 
                            ID_LOP_HOC,
                            MA_LOP_HOC,
                            TEN_LOP_HOC,
                            NGAY_BAT_DAU,
                            NGAY_KET_THUC,
                            SI_SO_TOI_DA,
                            ID_KHOA_HOC,
                            ID_GIANG_VIEN,
                            TRANG_THAI
                        FROM LOP_HOC WHERE ID_LOP_HOC = :id";
            return await ExecuteQuerySingleAsync<Class>(sql, new { id });
        }

        public async Task<ApiResponse<Class>> CreateClassAsync(ClassCreateDto dto)
        {
            try
            {
                // Check duplicate code
                var exists = Convert.ToInt32(await ExecuteScalarAsync("SELECT COUNT(*) FROM LOP_HOC WHERE MA_LOP_HOC = :code", new { code = dto.ClassCode }));
                if (exists > 0)
                {
                    return new ApiResponse<Class> { Success = false, Message = "Mã lớp đã tồn tại" };
                }

                var sql = @"INSERT INTO LOP_HOC (MA_LOP_HOC, TEN_LOP_HOC, NGAY_BAT_DAU, NGAY_KET_THUC, SI_SO_TOI_DA, ID_KHOA_HOC, ID_GIANG_VIEN, TRANG_THAI)
                             VALUES (:code, :name, :startdate, :enddate, :maxsize, :courseid, :teacherid, 'Đang tuyển sinh')";
                await ExecuteNonQueryAsync(sql, new
                {
                    code = dto.ClassCode,
                    name = dto.ClassName,
                    startdate = dto.StartDate,
                    enddate = dto.EndDate,
                    maxsize = dto.MaxSize,
                    courseid = dto.CourseId,
                    teacherid = dto.TeacherId
                });

                var cls = await ExecuteQuerySingleAsync<Class>(@"SELECT ID_LOP_HOC, MA_LOP_HOC, TEN_LOP_HOC, NGAY_BAT_DAU, NGAY_KET_THUC, SI_SO_TOI_DA, ID_KHOA_HOC, ID_GIANG_VIEN FROM LOP_HOC WHERE MA_LOP_HOC = :code", new { code = dto.ClassCode });
                return new ApiResponse<Class> { Success = true, Message = "Tạo lớp học thành công", Data = cls };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating class");
                return new ApiResponse<Class> { Success = false, Message = "Có lỗi xảy ra khi tạo lớp học" };
            }
        }

        public async Task<ApiResponse<Class>> UpdateClassAsync(ClassUpdateDto dto)
        {
            try
            {
                var entity = await GetClassByIdAsync(dto.ClassId);
                if (entity == null) return new ApiResponse<Class> { Success = false, Message = "Không tìm thấy lớp" };

                // Duplicate code check (exclude self)
                var exists = Convert.ToInt32(await ExecuteScalarAsync("SELECT COUNT(*) FROM LOP_HOC WHERE MA_LOP_HOC = :code AND ID_LOP_HOC != :id", new { code = dto.ClassCode, id = dto.ClassId }));
                if (exists > 0) return new ApiResponse<Class> { Success = false, Message = "Mã lớp đã tồn tại" };

                // Cập nhật trạng thái nếu được truyền (giữ nguyên nếu null)
                string sql;
                object parameters;
                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    sql = @"UPDATE LOP_HOC SET MA_LOP_HOC=:code, TEN_LOP_HOC=:name, NGAY_BAT_DAU=:startdate, NGAY_KET_THUC=:enddate, SI_SO_TOI_DA=:maxsize, ID_KHOA_HOC=:courseid, ID_GIANG_VIEN=:teacherid, TRANG_THAI=:status WHERE ID_LOP_HOC=:id";
                    parameters = new
                    {
                        code = dto.ClassCode,
                        name = dto.ClassName,
                        startdate = dto.StartDate,
                        enddate = dto.EndDate,
                        maxsize = dto.MaxSize,
                        courseid = dto.CourseId,
                        teacherid = dto.TeacherId,
                        status = dto.Status,
                        id = dto.ClassId
                    };
                }
                else
                {
                    sql = @"UPDATE LOP_HOC SET MA_LOP_HOC=:code, TEN_LOP_HOC=:name, NGAY_BAT_DAU=:startdate, NGAY_KET_THUC=:enddate, SI_SO_TOI_DA=:maxsize, ID_KHOA_HOC=:courseid, ID_GIANG_VIEN=:teacherid WHERE ID_LOP_HOC=:id";
                    parameters = new
                    {
                        code = dto.ClassCode,
                        name = dto.ClassName,
                        startdate = dto.StartDate,
                        enddate = dto.EndDate,
                        maxsize = dto.MaxSize,
                        courseid = dto.CourseId,
                        teacherid = dto.TeacherId,
                        id = dto.ClassId
                    };
                }
                await ExecuteNonQueryAsync(sql, parameters);

                var updated = await GetClassByIdAsync(dto.ClassId);
                return new ApiResponse<Class> { Success = true, Message = "Cập nhật lớp học thành công", Data = updated };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating class");
                return new ApiResponse<Class> { Success = false, Message = "Có lỗi xảy ra khi cập nhật lớp học" };
            }
        }

        public async Task<ApiResponse<bool>> DeleteClassAsync(int id)
        {
            try
            {
                // Check schedules or registrations referencing this class
                var cntReg = Convert.ToInt32(await ExecuteScalarAsync("SELECT COUNT(*) FROM DON_DANG_KY WHERE ID_LOP_HOC = :id", new { id }));
                if (cntReg > 0)
                    return new ApiResponse<bool> { Success = false, Message = "Không thể xóa lớp đã có đăng ký" };

                var cntSch = Convert.ToInt32(await ExecuteScalarAsync("SELECT COUNT(*) FROM LICH_HOC WHERE ID_LOP_HOC = :id", new { id }));
                if (cntSch > 0)
                    return new ApiResponse<bool> { Success = false, Message = "Không thể xóa lớp đã có lịch học" };

                var rows = await ExecuteNonQueryAsync("DELETE FROM LOP_HOC WHERE ID_LOP_HOC = :id", new { id });
                if (rows == 0) return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy lớp" };
                return new ApiResponse<bool> { Success = true, Message = "Xóa lớp học thành công", Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting class");
                return new ApiResponse<bool> { Success = false, Message = "Có lỗi xảy ra khi xóa lớp học" };
            }
        }

        public async Task<List<Student>> GetRosterAsync(int classId)
        {
            var sql = @"SELECT hv.ID_HOC_VIEN,
                                hv.HO_TEN,
                                hv.MA_HOC_VIEN,
                                hv.GIOI_TINH,
                                hv.NGAY_SINH,
                                hv.SO_DIEN_THOAI,
                                hv.DIA_CHI
                         FROM DON_DANG_KY dk
                         JOIN HOC_VIEN hv ON hv.ID_HOC_VIEN = dk.ID_HOC_VIEN
                         WHERE dk.ID_LOP_HOC = :cid AND dk.TRANG_THAI = N'Đã duyệt'";
            return await ExecuteQueryAsync<Student>(sql, new { cid = classId });
        }
    }
}
