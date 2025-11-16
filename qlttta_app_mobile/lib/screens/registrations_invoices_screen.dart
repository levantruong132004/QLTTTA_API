import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:qlttta_app_mobile/models/registration.dart';
import 'package:qlttta_app_mobile/models/invoice.dart';
import 'package:qlttta_app_mobile/services/registration_service.dart';
import 'package:qlttta_app_mobile/services/invoice_service.dart';
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
        return const Color(0xFF4CAF50);
      case 'pending':
      case 'chờ duyệt':
        return const Color(0xFFFF9800);
      case 'rejected':
      case 'từ chối':
        return const Color(0xFFF44336);
      default:
        return Colors.grey.shade600;
    }
  }

  Color _getInvoiceStatusColor(String status) {
    switch (status.toLowerCase()) {
      case 'paid':
      case 'đã thanh toán':
        return const Color(0xFF4CAF50);
      case 'pending_confirmation':
      case 'chờ xác nhận':
        return const Color(0xFFFF9800);
      case 'unpaid':
      case 'chưa thanh toán':
        return const Color(0xFFF44336);
      default:
        return Colors.grey.shade600;
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
            backgroundColor: result['success'] ? const Color(0xFF4CAF50) : const Color(0xFFF44336),
          ),
        );
        if (result['success']) _loadData();
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        elevation: 0,
        backgroundColor: Colors.transparent,
        leading: IconButton(
          icon: Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.white.withOpacity(0.2),
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(Icons.arrow_back_rounded, size: 20),
          ),
          onPressed: () => Navigator.of(context).pop(),
        ),
        title: const Text(
          'Đơn & Hóa đơn',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w600),
        ),
        actions: [
          IconButton(
            tooltip: 'Quét/Tra cứu QR',
            icon: Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.white.withOpacity(0.2),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(Icons.qr_code_scanner_rounded, size: 20),
            ),
            onPressed: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const QrScanRegistrationScreen()),
              );
            },
          ),
          const SizedBox(width: 8),
        ],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(48),
          child: Container(
            decoration: BoxDecoration(
              color: Colors.white.withOpacity(0.15),
              borderRadius: const BorderRadius.vertical(top: Radius.circular(20)),
            ),
            child: TabBar(
              controller: _tabController,
              indicatorColor: Colors.white,
              indicatorWeight: 3,
              labelColor: Colors.white,
              unselectedLabelColor: Colors.white70,
              labelStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
              tabs: const [
                Tab(text: 'ĐƠN ĐĂNG KÝ'),
                Tab(text: 'HÓA ĐƠN'),
              ],
            ),
          ),
        ),
      ),
      body: Container(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [
              const Color(0xFF8B4513).withOpacity(0.85),
              const Color(0xFFD2691E).withOpacity(0.75),
              const Color(0xFFF4A460).withOpacity(0.65),
            ],
          ),
        ),
        child: _isLoading
            ? const Center(child: CircularProgressIndicator(color: Colors.white))
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
      ),
    );
  }

  Widget _buildRegistrationsTab() {
    if (_registrations.isEmpty) {
      return Center(
        child: Container(
          margin: const EdgeInsets.symmetric(horizontal: 32),
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: Colors.white.withOpacity(0.9),
            borderRadius: BorderRadius.circular(20),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                Icons.assignment_outlined,
                size: 64,
                color: const Color(0xFF8B4513).withOpacity(0.6),
              ),
              const SizedBox(height: 16),
              Text(
                'Chưa có đơn đăng ký',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w600,
                  color: Colors.grey.shade700,
                ),
              ),
            ],
          ),
        ),
      );
    }

    return ListView.builder(
      padding: EdgeInsets.fromLTRB(16, MediaQuery.of(context).padding.top + 120, 16, 16),
      itemCount: _registrations.length,
      itemBuilder: (context, index) {
        final reg = _registrations[index];
        final statusColor = _getStatusColor(reg.status);

        return Container(
          margin: const EdgeInsets.only(bottom: 16),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withOpacity(0.1),
                blurRadius: 12,
                offset: const Offset(0, 4),
              ),
            ],
          ),
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
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF1a1a1a),
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                      decoration: BoxDecoration(
                        color: statusColor.withOpacity(0.15),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Text(
                        reg.getStatusText(),
                        style: TextStyle(
                          color: statusColor,
                          fontWeight: FontWeight.w600,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                const Divider(height: 1),
                const SizedBox(height: 12),
                _buildInfoRow(Icons.book_outlined, 'Khóa học', reg.courseName ?? '-'),
                if (reg.className != null)
                  _buildInfoRow(Icons.class_outlined, 'Lớp', reg.className!),
                _buildInfoRow(
                  Icons.calendar_today_rounded,
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
        child: Container(
          margin: const EdgeInsets.symmetric(horizontal: 32),
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: Colors.white.withOpacity(0.9),
            borderRadius: BorderRadius.circular(20),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                Icons.receipt_long_outlined,
                size: 64,
                color: const Color(0xFF8B4513).withOpacity(0.6),
              ),
              const SizedBox(height: 16),
              Text(
                'Chưa có hóa đơn',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w600,
                  color: Colors.grey.shade700,
                ),
              ),
            ],
          ),
        ),
      );
    }

    return ListView.builder(
      padding: EdgeInsets.fromLTRB(16, MediaQuery.of(context).padding.top + 120, 16, 16),
      itemCount: invoicesWithData.length,
      itemBuilder: (context, index) {
        final invoice = invoicesWithData[index];
        final statusColor = _getInvoiceStatusColor(invoice.status);
        final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: 'đ');

        return Container(
          margin: const EdgeInsets.only(bottom: 16),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withOpacity(0.1),
                blurRadius: 12,
                offset: const Offset(0, 4),
              ),
            ],
          ),
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
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF1a1a1a),
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                      decoration: BoxDecoration(
                        color: statusColor.withOpacity(0.15),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Text(
                        invoice.getStatusText(),
                        style: TextStyle(
                          color: statusColor,
                          fontWeight: FontWeight.w600,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                const Divider(height: 1),
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
                  Icons.event_rounded,
                  'Hạn thanh toán',
                  DateFormat('dd/MM/yyyy').format(invoice.dueDate),
                ),
                if (invoice.isOverdue)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Row(
                      children: [
                        const Icon(Icons.warning_rounded, size: 16, color: Color(0xFFF44336)),
                        const SizedBox(width: 4),
                        Text(
                          'Đã quá hạn',
                          style: const TextStyle(
                            color: Color(0xFFF44336),
                            fontWeight: FontWeight.w600,
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
                      height: 48,
                      child: ElevatedButton(
                        onPressed: () => _requestPayment(invoice),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: Colors.transparent,
                          foregroundColor: Colors.white,
                          padding: EdgeInsets.zero,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12),
                          ),
                          elevation: 0,
                          shadowColor: Colors.transparent,
                        ),
                        child: Ink(
                          decoration: BoxDecoration(
                            gradient: const LinearGradient(
                              colors: [Color(0xFF8B4513), Color(0xFFD2691E)],
                              begin: Alignment.centerLeft,
                              end: Alignment.centerRight,
                            ),
                            borderRadius: BorderRadius.circular(12),
                            boxShadow: [
                              BoxShadow(
                                color: const Color(0xFF8B4513).withOpacity(0.3),
                                blurRadius: 8,
                                offset: const Offset(0, 4),
                              ),
                            ],
                          ),
                          child: Container(
                            alignment: Alignment.center,
                            child: const Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Icon(Icons.payment_rounded, size: 20),
                                SizedBox(width: 8),
                                Text(
                                  'Xác nhận đã thanh toán',
                                  style: TextStyle(
                                    fontSize: 15,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ],
                            ),
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
          Icon(icon, size: 18, color: Colors.grey.shade600),
          const SizedBox(width: 8),
          Expanded(
            child: RichText(
              text: TextSpan(
                style: const TextStyle(fontSize: 14, color: Color(0xFF1a1a1a)),
                children: [
                  TextSpan(
                    text: '$label: ',
                    style: TextStyle(color: Colors.grey.shade600),
                  ),
                  TextSpan(
                    text: value,
                    style: const TextStyle(fontWeight: FontWeight.w600),
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
