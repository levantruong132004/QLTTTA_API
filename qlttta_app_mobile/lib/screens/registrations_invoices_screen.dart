import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:qlttta_app_mobile/models/registration.dart';
import 'package:qlttta_app_mobile/models/invoice.dart';
import 'package:qlttta_app_mobile/services/registration_service.dart';
import 'package:qlttta_app_mobile/services/invoice_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:qlttta_app_mobile/screens/qr_scan_registration_screen.dart';

class RegistrationsInvoicesScreen extends StatefulWidget {
  const RegistrationsInvoicesScreen({super.key});

  @override
  State<RegistrationsInvoicesScreen> createState() =>
      _RegistrationsInvoicesScreenState();
}

class _RegistrationsInvoicesScreenState
    extends State<RegistrationsInvoicesScreen> with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final RegistrationService _registrationService = RegistrationService();
  final InvoiceService _invoiceService = InvoiceService();
  
  List<Registration> _registrations = [];
  Map<int, Invoice?> _invoices = {}; // registrationId -> Invoice
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _loadData();
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    
    // Load registrations
    final registrations = await _registrationService.getMyRegistrations();
    
    // Load invoices for each registration
    Map<int, Invoice?> invoices = {};
    for (var reg in registrations) {
      final invoice = await _invoiceService.getByRegistration(reg.registrationId);
      invoices[reg.registrationId] = invoice;
    }
    
    setState(() {
      _registrations = registrations;
      _invoices = invoices;
      _isLoading = false;
    });
  }

  Color _getStatusColor(String status) {
    switch (status.toLowerCase()) {
      case 'approved':
      case 'đã duyệt':
        return RetroColors.success;
      case 'pending':
      case 'chờ duyệt':
        return RetroColors.warning;
      case 'rejected':
      case 'từ chối':
        return RetroColors.error;
      default:
        return RetroColors.textSecondary;
    }
  }

  Color _getInvoiceStatusColor(String status) {
    switch (status.toLowerCase()) {
      case 'paid':
      case 'đã thanh toán':
        return RetroColors.success;
      case 'pending_confirmation':
      case 'chờ xác nhận':
        return RetroColors.warning;
      case 'unpaid':
      case 'chưa thanh toán':
        return RetroColors.error;
      default:
        return RetroColors.textSecondary;
    }
  }

  Future<void> _requestPayment(Invoice invoice) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Xác nhận thanh toán'),
        content: Text(
          'Bạn đã thanh toán hóa đơn số ${invoice.invoiceId}?\n'
          'Số tiền: ${NumberFormat.currency(locale: 'vi_VN', symbol: 'đ').format(invoice.amount)}'
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Hủy'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Xác nhận'),
          ),
        ],
      ),
    );

    if (confirmed == true && mounted) {
      final result = await _invoiceService.requestPayment(invoice.invoiceId);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(result['message'] ?? 'Đã gửi yêu cầu'),
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
        title: const Text('ĐƠN & HÓA ĐƠN'),
        actions: [
          IconButton(
            tooltip: 'Quét/Tra cứu QR',
            icon: const Icon(Icons.qr_code_scanner_rounded),
            onPressed: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const QrScanRegistrationScreen()),
              );
            },
          ),
        ],
        bottom: TabBar(
          controller: _tabController,
          indicatorColor: RetroColors.accent,
          tabs: const [
            Tab(text: 'ĐƠN ĐĂNG KÝ'),
            Tab(text: 'HÓA ĐƠN'),
          ],
        ),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: RetroColors.primary))
          : RefreshIndicator(
              onRefresh: _loadData,
              child: TabBarView(
                controller: _tabController,
                children: [
                  _buildRegistrationsTab(),
                  _buildInvoicesTab(),
                ],
              ),
            ),
    );
  }

  Widget _buildRegistrationsTab() {
    if (_registrations.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.assignment_outlined, size: 64, color: RetroColors.textSecondary),
            const SizedBox(height: 16),
            Text(
              'Chưa có đơn đăng ký',
              style: TextStyle(fontSize: 16, color: RetroColors.textSecondary),
            ),
          ],
        ),
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.all(16),
      itemCount: _registrations.length,
      itemBuilder: (context, index) {
        final reg = _registrations[index];
        final statusColor = _getStatusColor(reg.status);

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
                      'Đơn #${reg.registrationId}',
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                      decoration: BoxDecoration(
                        color: statusColor.withValues(alpha: 0.15),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Text(
                        reg.getStatusText(),
                        style: TextStyle(
                          color: statusColor,
                          fontWeight: FontWeight.bold,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                _buildInfoRow(Icons.book_outlined, 'Khóa học', reg.courseName ?? '-'),
                if (reg.className != null)
                  _buildInfoRow(Icons.class_outlined, 'Lớp', reg.className!),
                _buildInfoRow(
                  Icons.calendar_today,
                  'Ngày đăng ký',
                  DateFormat('dd/MM/yyyy').format(reg.registrationDate),
                ),
                if (reg.notes != null && reg.notes!.isNotEmpty)
                  _buildInfoRow(Icons.note_outlined, 'Ghi chú', reg.notes!),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildInvoicesTab() {
    final invoicesWithData = _invoices.entries
        .where((entry) => entry.value != null)
        .map((entry) => entry.value!)
        .toList();

    if (invoicesWithData.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.receipt_long_outlined, size: 64, color: RetroColors.textSecondary),
            const SizedBox(height: 16),
            Text(
              'Chưa có hóa đơn',
              style: TextStyle(fontSize: 16, color: RetroColors.textSecondary),
            ),
          ],
        ),
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.all(16),
      itemCount: invoicesWithData.length,
      itemBuilder: (context, index) {
        final invoice = invoicesWithData[index];
        final statusColor = _getInvoiceStatusColor(invoice.status);
        final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: 'đ');

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
                      'Hóa đơn #${invoice.invoiceId}',
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                      decoration: BoxDecoration(
                        color: statusColor.withValues(alpha: 0.15),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Text(
                        invoice.getStatusText(),
                        style: TextStyle(
                          color: statusColor,
                          fontWeight: FontWeight.bold,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                _buildInfoRow(Icons.book_outlined, 'Khóa học', invoice.courseName ?? '-'),
                if (invoice.className != null)
                  _buildInfoRow(Icons.class_outlined, 'Lớp', invoice.className!),
                _buildInfoRow(
                  Icons.attach_money,
                  'Số tiền',
                  currencyFormat.format(invoice.amount),
                ),
                _buildInfoRow(
                  Icons.event,
                  'Hạn thanh toán',
                  DateFormat('dd/MM/yyyy').format(invoice.dueDate),
                ),
                if (invoice.isOverdue)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Row(
                      children: [
                        Icon(Icons.warning_rounded, size: 16, color: RetroColors.error),
                        const SizedBox(width: 4),
                        Text(
                          'Đã quá hạn',
                          style: TextStyle(
                            color: RetroColors.error,
                            fontWeight: FontWeight.bold,
                            fontSize: 12,
                          ),
                        ),
                      ],
                    ),
                  ),
                if (invoice.status.toLowerCase() == 'unpaid' ||
                    invoice.status.toLowerCase() == 'chưa thanh toán')
                  Padding(
                    padding: const EdgeInsets.only(top: 12),
                    child: SizedBox(
                      width: double.infinity,
                      child: ElevatedButton.icon(
                        onPressed: () => _requestPayment(invoice),
                        icon: const Icon(Icons.payment_rounded),
                        label: const Text('Xác nhận đã thanh toán'),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: RetroColors.primary,
                          foregroundColor: RetroColors.textLight,
                          padding: const EdgeInsets.symmetric(vertical: 12),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(8),
                          ),
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        );
      },
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
