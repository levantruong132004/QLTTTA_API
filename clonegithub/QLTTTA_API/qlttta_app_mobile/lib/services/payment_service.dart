import 'dart:convert';
import 'package:qlttta_app_mobile/models/payment.dart';
import 'package:qlttta_app_mobile/services/api_service.dart';

class PaymentService {
  final ApiService _apiService = ApiService();

  // Kế toán lấy danh sách chờ xác nhận
  Future<List<PendingPayment>> getPendingPayments() async {
    try {
      final response = await _apiService.get('Payments/pending');
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        if (body['success'] == true && body['data'] != null) {
          final List<dynamic> data = body['data'];
          return data.map((json) => PendingPayment.fromJson(json)).toList();
        }
      }
      return [];
    } catch (e) {
      print('Error fetching pending payments: $e');
      return [];
    }
  }

  // Kế toán xác nhận thanh toán
  Future<Map<String, dynamic>> confirmPayment(int invoiceId, int accountantId) async {
    try {
      final response = await _apiService.post('Payments/confirm', jsonEncode({
        'invoiceId': invoiceId,
        'accountantId': accountantId,
      }));

      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        return {'success': true, 'message': body['message'] ?? 'Xác nhận thành công'};
      }
      final body = jsonDecode(response.body);
      return {'success': false, 'message': body['message'] ?? 'Xác nhận thất bại'};
    } catch (e) {
      print('Error confirming payment: $e');
      return {'success': false, 'message': 'Lỗi kết nối: $e'};
    }
  }

  // Lấy thanh toán theo hóa đơn
  Future<List<Payment>> getByInvoice(int invoiceId) async {
    try {
      final response = await _apiService.get('Payments/by-invoice/$invoiceId');
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        if (body['success'] == true && body['data'] != null) {
          final List<dynamic> data = body['data'];
          return data.map((json) => Payment.fromJson(json)).toList();
        }
      }
      return [];
    } catch (e) {
      print('Error fetching payments by invoice: $e');
      return [];
    }
  }
}
