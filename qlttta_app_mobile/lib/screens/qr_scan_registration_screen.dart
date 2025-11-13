import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:qlttta_app_mobile/models/registration.dart';
import 'package:qlttta_app_mobile/services/registration_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';

class QrScanRegistrationScreen extends StatefulWidget {
  const QrScanRegistrationScreen({super.key});

  @override
  State<QrScanRegistrationScreen> createState() => _QrScanRegistrationScreenState();
}

class _QrScanRegistrationScreenState extends State<QrScanRegistrationScreen> {
  final MobileScannerController _controller = MobileScannerController();
  final RegistrationService _service = RegistrationService();
  bool _isProcessing = false;
  String? _lastCode;
  List<Registration> _results = [];

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _handleBarcode(BarcodeCapture capture) async {
    if (_isProcessing) return;
    final codes = capture.barcodes;
    if (codes.isEmpty) return;
    final raw = codes.first.rawValue;
    if (raw == null || raw.isEmpty) return;
    if (_lastCode == raw) return;
    setState(() {
      _isProcessing = true;
      _lastCode = raw;
    });

    final normalized = _normalizePayload(raw);
    final items = await _service.searchRegistrationsByQr(normalized);
    if (!mounted) return;
    setState(() {
      _results = items;
      _isProcessing = false;
    });
  }

  String _normalizePayload(String input) {
    var q = input.trim();
    // Common prefixes
    if (q.toUpperCase().startsWith('REG:')) {
      q = q.substring(4).trim();
    } else if (q.toUpperCase().startsWith('REGID:')) {
      q = q.substring(6).trim();
    }
    // If payload is JSON, try to extract code/id
    try {
      if ((q.startsWith('{') && q.endsWith('}')) || (q.startsWith('"') && q.endsWith('"')) ) {
        final obj = jsonDecode(q.trim().trimMatches('"'));
        if (obj is Map<String, dynamic>) {
          if (obj['registrationCode'] is String) return obj['registrationCode'];
          if (obj['regCode'] is String) return obj['regCode'];
          if (obj['code'] is String) return obj['code'];
          if (obj['registrationId'] != null) return obj['registrationId'].toString();
        }
      }
    } catch (_) {}
    return q;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        backgroundColor: RetroColors.primary,
        title: const Text('Quét QR - Tìm đăng ký'),
        actions: [
          IconButton(
            onPressed: () => _controller.toggleTorch(),
            icon: const Icon(Icons.flash_on_rounded),
            tooltip: 'Bật/tắt đèn',
          ),
          IconButton(
            onPressed: () => _controller.switchCamera(),
            icon: const Icon(Icons.cameraswitch_rounded),
            tooltip: 'Đổi camera',
          ),
        ],
      ),
      body: Column(
        children: [
          AspectRatio(
            aspectRatio: 1,
            child: MobileScanner(
              controller: _controller,
              onDetect: _handleBarcode,
            ),
          ),
          if (_isProcessing)
            const Padding(
              padding: EdgeInsets.all(12.0),
              child: LinearProgressIndicator(),
            ),
          Expanded(
            child: _results.isEmpty
                ? Center(
                    child: Text(
                      _lastCode == null
                          ? 'Hướng camera vào mã QR để tìm đơn đăng ký'
                          : 'Không tìm thấy kết quả cho: $_lastCode',
                      style: TextStyle(color: RetroColors.textSecondary),
                    ),
                  )
                : ListView.separated(
                    padding: const EdgeInsets.all(12),
                    separatorBuilder: (_, __) => const SizedBox(height: 8),
                    itemCount: _results.length,
                    itemBuilder: (_, i) => _regTile(_results[i]),
                  ),
          ),
        ],
      ),
    );
  }

  Widget _regTile(Registration r) {
    return Card(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: ListTile(
        title: Text(r.studentName ?? 'Học viên #${r.studentId}'),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (r.className != null) Text('Lớp: ${r.className}'),
            if (r.courseName != null) Text('Khóa: ${r.courseName}'),
            Text('Đơn #${r.registrationId} • ${r.getStatusText()}'),
          ],
        ),
        trailing: const Icon(Icons.chevron_right),
        onTap: () {
          // Pop with selected registration (optional)
          Navigator.pop(context, r);
        },
      ),
    );
  }
}

extension on String {
  String trimMatches(String pattern) {
    var s = this;
    while (s.startsWith(pattern)) s = s.substring(pattern.length);
    while (s.endsWith(pattern)) s = s.substring(0, s.length - pattern.length);
    return s;
  }
}
