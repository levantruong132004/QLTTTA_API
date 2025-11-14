import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:qlttta_app_mobile/models/registration.dart';
import 'package:qlttta_app_mobile/services/registration_service.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
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
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        backgroundColor: RetroColors.primary,
        title: const Text('Quét QR / Tìm đăng ký', style: TextStyle(fontSize: 16)),
        actions: [
          IconButton(
            onPressed: () async {
              // Bottom sheet để nhập nội dung tạo QR (mặc định lấy ô nhập tay hoặc mã quét gần nhất)
              final initial = _manualCtrl.text.trim().isNotEmpty ? _manualCtrl.text.trim() : (_lastCode ?? '');
              final ctrl = TextEditingController(text: initial);
              final payload = await showModalBottomSheet<String>(
                context: context,
                isScrollControlled: true,
                builder: (ctx) {
                  return Padding(
                    padding: EdgeInsets.only(
                      left: 16,
                      right: 16,
                      top: 16,
                      bottom: MediaQuery.of(ctx).viewInsets.bottom + 16,
                    ),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Nội dung mã QR'),
                        const SizedBox(height: 8),
                        TextField(
                          controller: ctrl,
                          decoration: const InputDecoration(
                            hintText: 'REG:/REGID:/CLASS: ... hoặc mã cần mã hóa',
                            border: OutlineInputBorder(),
                            isDense: true,
                          ),
                        ),
                        const SizedBox(height: 12),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.end,
                          children: [
                            TextButton(
                              onPressed: () => Navigator.pop(ctx),
                              child: const Text('Đóng'),
                            ),
                            const SizedBox(width: 8),
                            ElevatedButton.icon(
                              onPressed: () => Navigator.pop(ctx, ctrl.text.trim()),
                              icon: const Icon(Icons.qr_code_2_rounded),
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
                // Mở màn hình hiển thị QR
                // ignore: use_build_context_synchronously
                Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => QrGenerateScreen(payload: payload),
                  ),
                );
              }
            },
            icon: const Icon(Icons.qr_code_2_rounded),
            tooltip: 'Tạo QR',
          ),
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
            icon: const Icon(Icons.photo_library_outlined),
            tooltip: 'Quét từ ảnh',
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(12.0),
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _manualCtrl,
                    style: const TextStyle(fontSize: 14),
                    decoration: const InputDecoration(
                      hintText: 'Nhập mã đăng ký / lớp...',
                      hintStyle: TextStyle(fontSize: 13),
                      border: OutlineInputBorder(),
                      isDense: true,
                      contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                ElevatedButton.icon(
                  style: ElevatedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
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
                  label: const Text('Tìm', style: TextStyle(fontSize: 14)),
                )
              ],
            ),
          ),
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
    // Màu theo trạng thái
    Color statusColor;
    IconData statusIcon;
    final status = r.getStatusText().toLowerCase();
    
    if (status.contains('duyệt') || status.contains('approved')) {
      statusColor = RetroColors.success;
      statusIcon = Icons.check_circle;
    } else if (status.contains('chờ') || status.contains('pending')) {
      statusColor = RetroColors.warning;
      statusIcon = Icons.schedule;
    } else if (status.contains('từ chối') || status.contains('reject')) {
      statusColor = RetroColors.error;
      statusIcon = Icons.cancel;
    } else {
      statusColor = RetroColors.info;
      statusIcon = Icons.info;
    }

    return Card(
      elevation: 2,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: RetroColors.primary.withOpacity(0.2), width: 1),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () {
          Navigator.pop(context, r);
        },
        child: Padding(
          padding: const EdgeInsets.all(14.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Header: Tên học viên + Status badge
              Row(
                children: [
                  Expanded(
                    child: Row(
                      children: [
                        Icon(Icons.person, size: 18, color: RetroColors.primary),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            r.studentName ?? 'Học viên #${r.studentId}',
                            style: const TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.bold,
                              color: RetroColors.textPrimary,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: statusColor.withOpacity(0.15),
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: statusColor.withOpacity(0.3)),
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
              const SizedBox(height: 10),
              // Thông tin lớp & khóa
              if (r.className != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Row(
                    children: [
                      Icon(Icons.class_, size: 16, color: RetroColors.textSecondary),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          'Lớp: ${r.className}',
                          style: const TextStyle(
                            fontSize: 13,
                            color: RetroColors.textPrimary,
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
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Row(
                    children: [
                      Icon(Icons.menu_book, size: 16, color: RetroColors.textSecondary),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          'Khóa: ${r.courseName}',
                          style: const TextStyle(
                            fontSize: 13,
                            color: RetroColors.textPrimary,
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                  ),
                ),
              // Mã đơn
              Row(
                children: [
                  Icon(Icons.receipt_long, size: 16, color: RetroColors.textSecondary),
                  const SizedBox(width: 6),
                  Text(
                    'Đơn #${r.registrationId}',
                    style: const TextStyle(
                      fontSize: 13,
                      color: RetroColors.textSecondary,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  const Spacer(),
                  Icon(Icons.chevron_right, size: 20, color: RetroColors.primary.withOpacity(0.5)),
                ],
              ),
            ],
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
