import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:qlttta_app_mobile/models/payment.dart';
import 'package:qlttta_app_mobile/services/payment_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:shared_preferences/shared_preferences.dart';

class PendingPaymentsScreen extends StatefulWidget {
  const PendingPaymentsScreen({super.key});

  @override
  State<PendingPaymentsScreen> createState() => _PendingPaymentsScreenState();
}

class _PendingPaymentsScreenState extends State<PendingPaymentsScreen> {
  final PaymentService _paymentService = PaymentService();
  List<PendingPayment> _payments = [];
  bool _isLoading = false;
  int? _accountantId;

  @override
  void initState() {
    super.initState();
    _loadAccountantId();
    _loadData();
  }

  Future<void> _loadAccountantId() async {
    final prefs = await SharedPreferences.getInstance();
    setState(() {
      _accountantId = prefs.getInt('userId'); // Assuming userId is stored
    });
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    final payments = await _paymentService.getPendingPayments();
    setState(() {
      _payments = payments;
      _isLoading = false;
    });
  }

  Future<void> _confirmPayment(PendingPayment payment) async {
    if (_accountantId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Không tìm thấy thông tin kế toán'),
          backgroundColor: RetroColors.error,
        ),
      );
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Xác nhận thanh toán'),
        content: Text(
          'Xác nhận thanh toán hóa đơn #${payment.invoiceId}?\n'
          'Học viên: ${payment.studentName ?? "N/A"}\n'
          'Số tiền: ${NumberFormat.currency(locale: 'vi_VN', symbol: 'đ').format(payment.amount)}'
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Hủy'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(context, true),
            style: ElevatedButton.styleFrom(
              backgroundColor: RetroColors.success,
            ),
            child: const Text('Xác nhận'),
          ),
        ],
      ),
    );

    if (confirmed == true && mounted) {
      final result = await _paymentService.confirmPayment(
        payment.invoiceId,
        _accountantId!,
      );
      
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(result['message'] ?? 'Hoàn tất'),
            backgroundColor: result['success'] ? RetroColors.success : RetroColors.error,
          ),
        );
        if (result['success']) _loadData();
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        elevation: 0,
        backgroundColor: RetroColors.primary,
        title: const Text('THANH TOÁN CHỜ XÁC NHẬN'),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: RetroColors.primary))
          : RefreshIndicator(
              onRefresh: _loadData,
              child: _payments.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(Icons.payments_outlined,
                              size: 64, color: RetroColors.textSecondary),
                          const SizedBox(height: 16),
                          Text(
                            'Không có thanh toán chờ xác nhận',
                            style: TextStyle(
                                fontSize: 16, color: RetroColors.textSecondary),
                          ),
                        ],
                      ),
                    )
                  : ListView.builder(
                      padding: const EdgeInsets.all(16),
                      itemCount: _payments.length,
                      itemBuilder: (context, index) {
                        final payment = _payments[index];
                        return _buildPaymentCard(payment);
                      },
                    ),
            ),
    );
  }

  Widget _buildPaymentCard(PendingPayment payment) {
    final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: 'đ');
    final dateFormat = DateFormat('dd/MM/yyyy HH:mm');

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      elevation: 2,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Hóa đơn #${payment.invoiceId}',
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  decoration: BoxDecoration(
                    color: RetroColors.warning.withValues(alpha: 0.15),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(
                    'CHỜ XÁC NHẬN',
                    style: TextStyle(
                      color: RetroColors.warning,
                      fontWeight: FontWeight.bold,
                      fontSize: 12,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            _buildInfoRow(Icons.person_outlined, 'Học viên', payment.studentName ?? '-'),
            _buildInfoRow(Icons.book_outlined, 'Khóa học', payment.courseName ?? '-'),
            _buildInfoRow(Icons.attach_money, 'Số tiền', currencyFormat.format(payment.amount)),
            _buildInfoRow(
              Icons.schedule,
              'Yêu cầu lúc',
              dateFormat.format(payment.requestDate),
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton.icon(
                onPressed: () => _confirmPayment(payment),
                icon: const Icon(Icons.check_circle_outline),
                label: const Text('Xác nhận thanh toán'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: RetroColors.success,
                  foregroundColor: RetroColors.textLight,
                  padding: const EdgeInsets.symmetric(vertical: 12),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(8),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildInfoRow(IconData icon, String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: RetroColors.textSecondary),
          const SizedBox(width: 8),
          Expanded(
            child: RichText(
              text: TextSpan(
                style: TextStyle(fontSize: 14, color: RetroColors.textPrimary),
                children: [
                  TextSpan(
                    text: '$label: ',
                    style: TextStyle(color: RetroColors.textSecondary),
                  ),
                  TextSpan(
                    text: value,
                    style: const TextStyle(fontWeight: FontWeight.w500),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
