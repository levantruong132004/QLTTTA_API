import 'dart:typed_data';
import 'dart:ui' as ui;
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:qlttta_app_mobile/theme/retro_theme.dart';
import 'package:flutter/rendering.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';
import 'package:qlttta_app_mobile/services/qr_saver.dart';

class QrGenerateScreen extends StatelessWidget {
  final String payload;
  QrGenerateScreen({super.key, required this.payload});

  final GlobalKey _qrKey = GlobalKey();

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Scaffold(
      backgroundColor: RetroColors.background,
      appBar: AppBar(
        title: const Text('Tạo mã QR'),
        backgroundColor: RetroColors.primary,
        actions: [
          IconButton(
            tooltip: 'Lưu vào máy',
            icon: const Icon(Icons.save_alt_rounded),
            onPressed: () async {
              try {
                final boundary = _qrKey.currentContext?.findRenderObject() as RenderRepaintBoundary?;
                if (boundary == null) return;
                final image = await boundary.toImage(pixelRatio: 3.0);
                final byteData = await image.toByteData(format: ui.ImageByteFormat.png);
                if (byteData == null) return;
                final pngBytes = byteData.buffer.asUint8List();
                final ok = await QrSaver.savePngBytes(pngBytes);
                // ignore: use_build_context_synchronously
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(content: Text(ok ? 'Đã lưu mã QR' : 'Lưu thất bại')),);
              } catch (e) {
                // ignore: use_build_context_synchronously
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(content: Text('Lỗi lưu: $e')),
                );
              }
            },
          ),
          IconButton(
            tooltip: 'Chia sẻ',
            icon: const Icon(Icons.share_rounded),
            onPressed: () async {
              try {
                final boundary = _qrKey.currentContext?.findRenderObject() as RenderRepaintBoundary?;
                if (boundary == null) return;
                final image = await boundary.toImage(pixelRatio: 3.0);
                final byteData = await image.toByteData(format: ui.ImageByteFormat.png);
                if (byteData == null) return;
                final pngBytes = byteData.buffer.asUint8List();
                final dir = await getTemporaryDirectory();
                final file = File('${dir.path}/qr_share_${DateTime.now().millisecondsSinceEpoch}.png');
                await file.writeAsBytes(pngBytes, flush: true);
                await Share.shareXFiles([XFile(file.path)], text: 'QR đăng ký');
              } catch (e) {
                // ignore: use_build_context_synchronously
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(content: Text('Lỗi khi chia sẻ: $e')),
                );
              }
            },
          ),
        ],
      ),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            Expanded(
              child: Center(
                child: Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(16),
                    boxShadow: [
                      BoxShadow(
                        color: Colors.black.withOpacity(0.05),
                        blurRadius: 12,
                        offset: const Offset(0, 8),
                      )
                    ],
                  ),
                  child: RepaintBoundary(
                    key: _qrKey,
                    child: QrImageView(
                      data: payload,
                      size: 260,
                      backgroundColor: Colors.white,
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(height: 12),
            Align(
              alignment: Alignment.centerLeft,
              child: Text('Nội dung:', style: textTheme.titleSmall?.copyWith(color: RetroColors.textSecondary)),
            ),
            const SizedBox(height: 6),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: RetroColors.surface,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(
                payload,
                style: textTheme.bodyMedium?.copyWith(color: RetroColors.textPrimary),
              ),
            ),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
  }
}
