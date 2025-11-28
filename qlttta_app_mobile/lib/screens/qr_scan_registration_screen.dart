import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:qlttta_app_mobile/models/registration.dart';
import 'package:qlttta_app_mobile/services/registration_service.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:qlttta_app_mobile/screens/qr_generate_screen.dart';
import 'package:image_picker/image_picker.dart';
import 'package:qlttta_app_mobile/services/profile_service.dart';
import 'package:image/image.dart' as img;
import 'package:zxing2/qrcode.dart';

class QrScanRegistrationScreen extends StatefulWidget {
  const QrScanRegistrationScreen({super.key});

  @override
  State<QrScanRegistrationScreen> createState() => _QrScanRegistrationScreenState();
}

class _QrScanRegistrationScreenState extends State<QrScanRegistrationScreen> {
  final MobileScannerController _controller = MobileScannerController();
  final RegistrationService _service = RegistrationService();
  // ProfileService no longer needed for mine-only filtering (handled server-side)
  bool _isProcessing = false;
  String? _lastCode;
  List<Registration> _results = [];
  final TextEditingController _manualCtrl = TextEditingController();
  bool _forceProcessNext = false; // Khi chọn ảnh từ thư viện, cho phép xử lý lại mã trùng
  int _galleryCheckCounter = 0; // Dùng để xác định vòng kiểm tra sau chọn ảnh

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
    // Nếu mã trùng và không ép xử lý thì bỏ qua, ngược lại vẫn xử lý
    if (_lastCode == raw && !_forceProcessNext) return;
    setState(() {
      _isProcessing = true;
      _lastCode = raw;
      _forceProcessNext = false; // reset cờ sau khi đã kích hoạt xử lý
    });

    final normalized = _normalizePayload(raw);
    final items = await _searchAndFilter(normalized);
    if (!mounted) return;
    setState(() {
      _results = items;
      _isProcessing = false;
    });
    if (items.isEmpty && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Không tìm thấy kết quả cho mã: $normalized')),
      );
    }
  }

  Future<List<Registration>> _searchAndFilter(String query) async {
    final prefs = await SharedPreferences.getInstance();
    final roleId = prefs.getInt('roleId') ?? 0;
    final isStudent = roleId == 1;
    // 1) Thử yêu cầu server lọc mine-only
    final first = await _service.searchRegistrationsByQr(query, mine: isStudent);
    if (isStudent && first.isEmpty) {
      // 2) Fallback: lấy full rồi lọc client theo studentId từ profile
      final all = await _service.searchRegistrationsByQr(query);
      try {
        final sid = await ProfileService().getCurrentStudentId();
        if (sid != null && sid > 0) {
          return all.where((e) => e.studentId == sid).toList();
        }
      } catch (_) {}
      return <Registration>[];
    }
    return first;
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
          'Quét QR / Tìm đăng ký',
          style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
        ),
        actions: [
          IconButton(
            onPressed: () async {
              final initial = _manualCtrl.text.trim().isNotEmpty ? _manualCtrl.text.trim() : (_lastCode ?? '');
              final ctrl = TextEditingController(text: initial);
              final payload = await showModalBottomSheet<String>(
                context: context,
                isScrollControlled: true,
                backgroundColor: Colors.white,
                shape: const RoundedRectangleBorder(
                  borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
                ),
                builder: (ctx) {
                  return Padding(
                    padding: EdgeInsets.only(
                      left: 20,
                      right: 20,
                      top: 20,
                      bottom: MediaQuery.of(ctx).viewInsets.bottom + 20,
                    ),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Nội dung mã QR',
                          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                        ),
                        const SizedBox(height: 12),
                        TextField(
                          controller: ctrl,
                          decoration: InputDecoration(
                            hintText: 'REG:/REGID:/CLASS: ... hoặc mã cần mã hóa',
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12),
                            ),
                            filled: true,
                            fillColor: Colors.grey.shade50,
                          ),
                        ),
                        const SizedBox(height: 16),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.end,
                          children: [
                            TextButton(
                              onPressed: () => Navigator.pop(ctx),
                              child: const Text('Đóng'),
                            ),
                            const SizedBox(width: 8),
                            ElevatedButton.icon(
                              style: ElevatedButton.styleFrom(
                                backgroundColor: const Color(0xFF8B4513),
                                foregroundColor: Colors.white,
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(12),
                                ),
                                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                              ),
                              onPressed: () => Navigator.pop(ctx, ctrl.text.trim()),
                              icon: const Icon(Icons.qr_code_2_rounded, size: 20),
                              label: const Text('Tạo QR'),
                            ),
                          ],
                        )
                      ],
                    ),
                  );
                },
              );
              if (payload != null && payload.isNotEmpty && context.mounted) {
                Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => QrGenerateScreen(payload: payload),
                  ),
                );
              }
            },
            icon: Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.white.withOpacity(0.2),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(Icons.qr_code_2_rounded, size: 20),
            ),
            tooltip: 'Tạo QR',
          ),
          IconButton(
            onPressed: () => _controller.toggleTorch(),
            icon: Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.white.withOpacity(0.2),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(Icons.flash_on_rounded, size: 20),
            ),
            tooltip: 'Bật/tắt đèn',
          ),
          IconButton(
            onPressed: () => _controller.switchCamera(),
            icon: Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.white.withOpacity(0.2),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(Icons.cameraswitch_rounded, size: 20),
            ),
            tooltip: 'Đổi camera',
          ),
          IconButton(
            onPressed: () async {
              // Chọn ảnh từ thư viện và phân tích QR
              try {
                final picker = ImagePicker();
                final img = await picker.pickImage(source: ImageSource.gallery);
                if (img == null) return;
                // Ép xử lý kể cả khi mã vừa quét trước đó
                _forceProcessNext = true;
                final prevLast = _lastCode; // ghi nhận để kiểm tra thay đổi
                final checkId = ++_galleryCheckCounter;
                await _controller.analyzeImage(img.path);
                // Fallback: nếu sau một khoảng ngắn vẫn chưa có kết quả hoặc mã không thay đổi, hiển thị thông báo
                Future.delayed(const Duration(milliseconds: 600), () {
                  if (!mounted) return;
                  // Chỉ chạy nếu vẫn cùng vòng kiểm tra
                  if (checkId != _galleryCheckCounter) return;
                  if (_isProcessing) return; // vẫn còn xử lý
                  // Không decode ra mã mới
                  final noNewCode = _lastCode == null || _lastCode == prevLast;
                  if (noNewCode) {
                    _fallbackDecode(img.path, prevLast);
                  } else if (_results.isEmpty) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Mã QR hợp lệ nhưng không tìm thấy kết quả đăng ký')),
                    );
                  }
                });
              } catch (e) {
                if (!mounted) return;
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(content: Text('Không thể đọc ảnh: $e')),
                );
              }
            },
            icon: Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.white.withOpacity(0.2),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(Icons.photo_library_outlined, size: 20),
            ),
            tooltip: 'Quét từ ảnh',
          ),
        ],
      ),
      body: Container(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [
              const Color(0xFF8B4513).withOpacity(0.9),
              const Color(0xFFD2691E).withOpacity(0.7),
              const Color(0xFFF4A460).withOpacity(0.5),
            ],
          ),
        ),
        child: Column(
          children: [
            SizedBox(height: MediaQuery.of(context).padding.top + 60),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              child: Row(
                children: [
                  Expanded(
                    child: Container(
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
                      child: TextField(
                        controller: _manualCtrl,
                        style: const TextStyle(fontSize: 14),
                        decoration: InputDecoration(
                          hintText: 'Nhập mã đăng ký / lớp...',
                          hintStyle: TextStyle(fontSize: 13, color: Colors.grey.shade400),
                          border: InputBorder.none,
                          contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF8B4513),
                      foregroundColor: Colors.white,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                      elevation: 2,
                    ),
                    onPressed: () async {
                      final q = _manualCtrl.text.trim();
                      if (q.isEmpty) return;
                      setState(() { _isProcessing = true; _lastCode = q; });
                      final items = await _searchAndFilter(q);
                      if (!mounted) return;
                      setState(() { _results = items; _isProcessing = false; });
                    },
                    icon: const Icon(Icons.search, size: 18),
                    label: const Text('Tìm', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600)),
                  ),
                ],
              ),
            ),
            Container(
              margin: const EdgeInsets.symmetric(horizontal: 16),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: Colors.white.withOpacity(0.3), width: 3),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withOpacity(0.2),
                    blurRadius: 20,
                    offset: const Offset(0, 8),
                  ),
                ],
              ),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(17),
                child: AspectRatio(
                  aspectRatio: 1,
                  child: MobileScanner(
                    controller: _controller,
                    onDetect: _handleBarcode,
                  ),
                ),
              ),
            ),
            if (_isProcessing)
              Padding(
                padding: const EdgeInsets.all(16.0),
                child: LinearProgressIndicator(
                  backgroundColor: Colors.white.withOpacity(0.3),
                  valueColor: const AlwaysStoppedAnimation<Color>(Colors.white),
                  borderRadius: BorderRadius.circular(4),
                ),
              ),
            Expanded(
              child: _results.isEmpty
                  ? Center(
                      child: Container(
                        margin: const EdgeInsets.symmetric(horizontal: 32),
                        padding: const EdgeInsets.all(20),
                        decoration: BoxDecoration(
                          color: Colors.white.withOpacity(0.9),
                          borderRadius: BorderRadius.circular(16),
                        ),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(
                              _lastCode == null ? Icons.qr_code_scanner_rounded : Icons.search_off_rounded,
                              size: 48,
                              color: const Color(0xFF8B4513).withOpacity(0.6),
                            ),
                            const SizedBox(height: 12),
                            Text(
                              _lastCode == null
                                  ? 'Hướng camera vào mã QR để tìm đơn đăng ký'
                                  : 'Không tìm thấy kết quả cho: $_lastCode',
                              style: TextStyle(
                                color: Colors.grey.shade700,
                                fontSize: 14,
                              ),
                              textAlign: TextAlign.center,
                            ),
                          ],
                        ),
                      ),
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.all(16),
                      separatorBuilder: (_, __) => const SizedBox(height: 12),
                      itemCount: _results.length,
                      itemBuilder: (_, i) => _regTile(_results[i]),
                    ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _regTile(Registration r) {
    Color statusColor;
    IconData statusIcon;
    final status = r.getStatusText().toLowerCase();
    
    if (status.contains('duyệt') || status.contains('approved')) {
      statusColor = const Color(0xFF4CAF50);
      statusIcon = Icons.check_circle_rounded;
    } else if (status.contains('chờ') || status.contains('pending')) {
      statusColor = const Color(0xFFFF9800);
      statusIcon = Icons.schedule_rounded;
    } else if (status.contains('từ chối') || status.contains('reject')) {
      statusColor = const Color(0xFFF44336);
      statusIcon = Icons.cancel_rounded;
    } else {
      statusColor = const Color(0xFF2196F3);
      statusIcon = Icons.info_rounded;
    }

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.08),
            blurRadius: 12,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: BorderRadius.circular(16),
          onTap: () {
            Navigator.pop(context, r);
          },
          child: Padding(
            padding: const EdgeInsets.all(16.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: const Color(0xFF8B4513).withOpacity(0.1),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Icon(Icons.person_rounded, size: 20, color: const Color(0xFF8B4513)),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        r.studentName ?? 'Học viên #${r.studentId}',
                        style: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF1a1a1a),
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                      decoration: BoxDecoration(
                        color: statusColor.withOpacity(0.15),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(statusIcon, size: 14, color: statusColor),
                          const SizedBox(width: 4),
                          Text(
                            r.getStatusText(),
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: statusColor,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                if (r.className != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: Row(
                      children: [
                        Icon(Icons.class_rounded, size: 16, color: Colors.grey.shade600),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            r.className!,
                            style: TextStyle(
                              fontSize: 14,
                              color: Colors.grey.shade800,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                  ),
                if (r.courseName != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: Row(
                      children: [
                        Icon(Icons.menu_book_rounded, size: 16, color: Colors.grey.shade600),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            r.courseName!,
                            style: TextStyle(
                              fontSize: 14,
                              color: Colors.grey.shade800,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                  ),
                Row(
                  children: [
                    Icon(Icons.receipt_long_rounded, size: 16, color: Colors.grey.shade600),
                    const SizedBox(width: 8),
                    Text(
                      'Đơn #${r.registrationId}',
                      style: TextStyle(
                        fontSize: 13,
                        color: Colors.grey.shade600,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                    const Spacer(),
                    Icon(Icons.arrow_forward_ios_rounded, size: 16, color: const Color(0xFF8B4513).withOpacity(0.5)),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
  Future<void> _fallbackDecode(String path, String? prevLast) async {
    try {
      final bytes = await File(path).readAsBytes();
      final decoded = img.decodeImage(bytes);
      if (decoded == null) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Không đọc được ảnh (decode thất bại)')),
        );
        return;
      }
      final converted = decoded.convert(numChannels: 4);
      final int32 = converted
          .getBytes(order: img.ChannelOrder.abgr)
          .buffer
          .asInt32List();
      final source = RGBLuminanceSource(decoded.width, decoded.height, int32);
      final bitmap = BinaryBitmap(GlobalHistogramBinarizer(source));
      final reader = QRCodeReader();
      Result result;
      try {
        result = reader.decode(bitmap);
      } on ReaderException {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Không tìm thấy mã QR trong ảnh đã chọn')),
        );
        return;
      }
      final text = result.text.trim();
      if (text.isEmpty || text == prevLast) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Không đọc được mã QR trong ảnh đã chọn')),
        );
        return;
      }
      setState(() {
        _isProcessing = true;
        _lastCode = text;
      });
      final normalized = _normalizePayload(text);
      final items = await _searchAndFilter(normalized);
      if (!mounted) return;
      setState(() {
        _results = items;
        _isProcessing = false;
      });
      if (items.isEmpty) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Mã QR hợp lệ nhưng không có đăng ký: $normalized')),
        );
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Lỗi giải mã QR fallback: $e')),
      );
    }
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
