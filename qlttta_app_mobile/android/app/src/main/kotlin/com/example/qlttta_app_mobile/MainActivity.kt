package com.example.qlttta_app_mobile

import android.content.ContentValues
import android.graphics.BitmapFactory
import android.os.Build
import android.provider.MediaStore
import android.widget.Toast
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.OutputStream

class MainActivity : FlutterActivity() {
	private val CHANNEL = "qr_saver"

	override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
		super.configureFlutterEngine(flutterEngine)

		MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL).setMethodCallHandler { call, result ->
			when (call.method) {
				"saveImage" -> {
					try {
						val bytes = call.argument<ByteArray>("bytes")
						if (bytes == null || bytes.isEmpty()) {
							result.error("NO_BYTES", "Empty image data", null)
							return@setMethodCallHandler
						}
						val displayName = "qr_${System.currentTimeMillis()}.png"
						val resolver = applicationContext.contentResolver
						val contentValues = ContentValues().apply {
							put(MediaStore.Images.Media.DISPLAY_NAME, displayName)
							put(MediaStore.Images.Media.MIME_TYPE, "image/png")
							if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
								put(MediaStore.Images.Media.IS_PENDING, 1)
							}
						}
						val uri = resolver.insert(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, contentValues)
						if (uri == null) {
							result.error("INSERT_FAIL", "Cannot create MediaStore entry", null)
							return@setMethodCallHandler
						}
						resolver.openOutputStream(uri).use { out: OutputStream? ->
							if (out == null) {
								result.error("STREAM_FAIL", "Cannot open output stream", null)
								return@setMethodCallHandler
							}
							out.write(bytes)
						}
						if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
							contentValues.clear()
							contentValues.put(MediaStore.Images.Media.IS_PENDING, 0)
							resolver.update(uri, contentValues, null, null)
						}
						Toast.makeText(applicationContext, "Đã lưu QR", Toast.LENGTH_SHORT).show()
						result.success(true)
					} catch (e: Exception) {
						result.error("EX", e.message, null)
					}
				}
				else -> result.notImplemented()
			}
		}
	}
}
