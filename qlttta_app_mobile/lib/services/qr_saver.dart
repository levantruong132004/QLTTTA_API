import 'package:flutter/services.dart';

class QrSaver {
  static const MethodChannel _chan = MethodChannel('qr_saver');

  static Future<bool> savePngBytes(List<int> bytes) async {
    try {
      final ok = await _chan.invokeMethod('saveImage', {'bytes': bytes});
      return ok == true;
    } catch (_) {
      return false;
    }
  }
}
