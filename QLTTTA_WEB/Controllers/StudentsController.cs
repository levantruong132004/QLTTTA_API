using Microsoft.AspNetCore.Mvc;
using QLTTTA_WEB.Models;
using System.Text;
using System.Text.Json;

namespace QLTTTA_WEB.Controllers
{
    public class StudentsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<StudentsController> _logger;

        public StudentsController(IHttpClientFactory httpClientFactory, ILogger<StudentsController> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ApiClient");
            _logger = logger;
        }

        private bool CheckAuthentication()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"));
        }

        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            if (!CheckAuthentication())
                return RedirectToAction("Login", "Account");

            try
            {
                var queryString = $"?pageNumber={pageNumber}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(search))
                    queryString += $"&search={Uri.EscapeDataString(search)}";

                var response = await _httpClient.GetAsync($"api/students{queryString}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<PaginatedApiResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    var viewModel = new PaginatedViewModel<StudentViewModel>
                    {
                        Items = apiResponse?.Data?.Select(MapToStudentViewModel).ToList() ?? new(),
                        TotalRecords = apiResponse?.TotalRecords ?? 0,
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        SearchTerm = search
                    };

                    return View(viewModel);
                }

                TempData["ErrorMessage"] = "Không thể tải danh sách học viên";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading students");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải danh sách học viên";
            }

            return View(new PaginatedViewModel<StudentViewModel>());
        }

        public async Task<IActionResult> Details(int id)
        {
            if (!CheckAuthentication())
                return RedirectToAction("Login", "Account");

            try
            {
                var response = await _httpClient.GetAsync($"api/students/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<StudentApiResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse?.Success == true && apiResponse.Data != null)
                    {
                        return View(MapToStudentViewModel(apiResponse.Data));
                    }
                }

                TempData["ErrorMessage"] = "Không tìm thấy học viên";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student details");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải thông tin học viên";
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Create()
        {
            if (!CheckAuthentication())
                return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentCreateViewModel model)
        {
            if (!CheckAuthentication())
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var createDto = new
                {
                    FullName = model.FullName,
                    StudentCode = model.StudentCode,
                    Sex = model.Sex,
                    DateOfBirth = model.DateOfBirth,
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address
                };

                var json = JsonSerializer.Serialize(createDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/students", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<StudentApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true)
                {
                    TempData["SuccessMessage"] = "Tạo học viên thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError("", apiResponse?.Message ?? "Có lỗi xảy ra khi tạo học viên");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating student");
                ModelState.AddModelError("", "Có lỗi xảy ra khi tạo học viên");
            }

            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!CheckAuthentication())
                return RedirectToAction("Login", "Account");

            try
            {
                var response = await _httpClient.GetAsync($"api/students/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<StudentApiResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse?.Success == true && apiResponse.Data != null)
                    {
                        var student = apiResponse.Data;
                        var viewModel = new StudentEditViewModel
                        {
                            StudentId = student.StudentId,
                            FullName = student.FullName ?? "",
                            StudentCode = student.StudentCode ?? "",
                            Sex = student.Sex,
                            DateOfBirth = student.DateOfBirth ?? DateTime.Now,
                            PhoneNumber = student.PhoneNumber ?? "",
                            Address = student.Address
                        };

                        return View(viewModel);
                    }
                }

                TempData["ErrorMessage"] = "Không tìm thấy học viên";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student for edit");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải thông tin học viên";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StudentEditViewModel model)
        {
            if (!CheckAuthentication())
                return RedirectToAction("Login", "Account");

            if (id != model.StudentId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var updateDto = new
                {
                    StudentId = model.StudentId,
                    FullName = model.FullName,
                    StudentCode = model.StudentCode,
                    Sex = model.Sex,
                    DateOfBirth = model.DateOfBirth,
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address
                };

                var json = JsonSerializer.Serialize(updateDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"api/students/{id}", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<StudentApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true)
                {
                    TempData["SuccessMessage"] = "Cập nhật học viên thành công!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError("", apiResponse?.Message ?? "Có lỗi xảy ra khi cập nhật học viên");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating student");
                ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật học viên");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!CheckAuthentication())
                return RedirectToAction("Login", "Account");

            try
            {
                var response = await _httpClient.DeleteAsync($"api/students/{id}");
                var responseContent = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true)
                {
                    TempData["SuccessMessage"] = "Xóa học viên thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = apiResponse?.Message ?? "Có lỗi xảy ra khi xóa học viên";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa học viên";
            }

            return RedirectToAction(nameof(Index));
        }

        private StudentViewModel MapToStudentViewModel(StudentData student)
        {
            return new StudentViewModel
            {
                StudentId = student.StudentId,
                FullName = student.FullName ?? "",
                StudentCode = student.StudentCode ?? "",
                Sex = student.Sex,
                DateOfBirth = student.DateOfBirth,
                PhoneNumber = student.PhoneNumber ?? "",
                Address = student.Address
            };
        }
    }

    // API Response Classes
    public class PaginatedApiResponse
    {
        public List<StudentData> Data { get; set; } = new();
        public int TotalRecords { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public class StudentApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public StudentData? Data { get; set; }
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public class StudentData
    {
        public int StudentId { get; set; }
        public string? FullName { get; set; }
        public string? StudentCode { get; set; }
        public string? Sex { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
    }
}