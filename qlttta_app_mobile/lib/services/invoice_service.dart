import 'dart:convert';
import 'package:qlttta_app_mobile/models/invoice.dart';
import 'package:qlttta_app_mobile/services/api_service.dart';

class InvoiceService {
  final ApiService _apiService = ApiService();

  // Lấy hóa đơn theo đơn đăng ký
  Future<Invoice?> getByRegistration(int registrationId) async {
    try {
      final response = await _apiService.get('Invoices/by-registration/$registrationId');
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        if (body['success'] == true && body['data'] != null) {
          final data = body['data'];
          if (data['invoice'] != null) {
            return Invoice.fromJson(data['invoice']);
          }
          return Invoice.fromJson(data);
        }
      }
      return null;
    } catch (e) {
      print('Error fetching invoice by registration: $e');
      return null;
    }
  }

  // Tạo hóa đơn (Kế toán)
  Future<Map<String, dynamic>> createInvoice({
    required int registrationId,
    required DateTime dueDate,
    required int amount,
  }) async {
    try {
      final response = await _apiService.post('Invoices', jsonEncode({
        'registrationId': registrationId,
        'dueDate': dueDate.toIso8601String(),
        'amount': amount,
      }));

      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        return {
          'success': true,
          'message': body['message'] ?? 'Tạo hóa đơn thành công',
          'data': body['data'],
        };
      }
      final body = jsonDecode(response.body);
      return {'success': false, 'message': body['message'] ?? 'Tạo hóa đơn thất bại'};
    } catch (e) {
      print('Error creating invoice: $e');
      return {'success': false, 'message': 'Lỗi kết nối: $e'};
    }
  }

  // Học viên yêu cầu xác nhận thanh toán
  Future<Map<String, dynamic>> requestPayment(int invoiceId) async {
    try {
      final response = await _apiService.post('Payments/student-request/$invoiceId', '{}');
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        return {'success': true, 'message': body['message'] ?? 'Gửi yêu cầu thành công'};
      }
      final body = jsonDecode(response.body);
      return {'success': false, 'message': body['message'] ?? 'Gửi yêu cầu thất bại'};
    } catch (e) {
      print('Error requesting payment: $e');
      return {'success': false, 'message': 'Lỗi kết nối: $e'};
    }
  }
}
