import 'package:flutter/material.dart';

class RetroColors {
  // Modern vintage palette - warmer, more contemporary
  static const Color primary = Color(0xFF8B4513); // SaddleBrown
  static const Color primaryDark = Color(0xFF5D2E0E);
  static const Color primaryLight = Color(0xFFB8734A);
  
  // Background & Surface
  static const Color background = Color(0xFFFAF6F1);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color surfaceLight = Color(0xFFF8F4EF);
  
  // Accent colors - modernized
  static const Color accent = Color(0xFFD4A574); // Golden tan
  static const Color accentDark = Color(0xFFA67C52);
  static const Color success = Color(0xFF6B8E23); // OliveDrab
  static const Color warning = Color(0xFFE67E22); // Carrot
  static const Color error = Color(0xFFB8001F); // Deep red
  static const Color info = Color(0xFF5A7D9A); // Steel blue
  
  // Text colors
  static const Color textPrimary = Color(0xFF2C1810);
  static const Color textSecondary = Color(0xFF6B5D52);
  static const Color textHint = Color(0xFF9B8D82);
  static const Color textLight = Color(0xFFFAF6F1);
  
  // Legacy colors (keep for backwards compatibility)
  static const Color vintageBrown = primary;
  static const Color vintageDarkBrown = primaryDark;
  static const Color vintageLightBrown = primaryLight;
  static const Color vintageCream = background;
  static const Color vintageBeige = surfaceLight;
  static const Color vintageTan = accent;
  static const Color vintageGold = accentDark;
  static const Color vintageRust = warning;
  static const Color vintageOlive = success;
  static const Color vintageBurgundy = error;
  static const Color vintageGreen = success;
  static const Color vintageWhite = surface;
  static const Color vintageOffWhite = surfaceLight;
  static const Color vintageGray = textSecondary;
  static const Color vintageDarkGray = textPrimary;
}

class RetroTheme {
  static ThemeData get theme {
    return ThemeData(
      primaryColor: RetroColors.vintageBrown,
      scaffoldBackgroundColor: RetroColors.vintageCream,

      // AppBar Theme
      appBarTheme: const AppBarTheme(
        backgroundColor: RetroColors.vintageDarkBrown,
        foregroundColor: RetroColors.vintageCream,
        elevation: 4,
        titleTextStyle: TextStyle(
          fontSize: 18,
          fontWeight: FontWeight.bold,
          letterSpacing: 2,
          color: RetroColors.vintageCream,
        ),
      ),

      // Elevated Button Theme
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: RetroColors.vintageBrown,
          foregroundColor: RetroColors.vintageCream,
          elevation: 0,
          shape: const RoundedRectangleBorder(
            borderRadius: BorderRadius.zero,
            side: BorderSide(
              color: RetroColors.vintageDarkBrown,
              width: 3,
            ),
          ),
          padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 16),
          textStyle: const TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.bold,
            letterSpacing: 2,
          ),
        ),
      ),

      // Input Decoration Theme
      inputDecorationTheme: const InputDecorationTheme(
        filled: true,
        fillColor: RetroColors.vintageWhite,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.zero,
          borderSide: BorderSide(
            color: RetroColors.vintageBrown,
            width: 2,
          ),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.zero,
          borderSide: BorderSide(
            color: RetroColors.vintageBrown,
            width: 2,
          ),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.zero,
          borderSide: BorderSide(
            color: RetroColors.vintageRust,
            width: 2,
          ),
        ),
        labelStyle: TextStyle(
          fontWeight: FontWeight.bold,
          color: RetroColors.vintageDarkBrown,
        ),
      ),

      // Text Theme
      textTheme: const TextTheme(
        headlineLarge: TextStyle(
          fontSize: 32,
          fontWeight: FontWeight.bold,
          color: RetroColors.vintageBrown,
          letterSpacing: 2,
        ),
        headlineMedium: TextStyle(
          fontSize: 24,
          fontWeight: FontWeight.bold,
          color: RetroColors.vintageRust,
          letterSpacing: 2,
        ),
        bodyLarge: TextStyle(
          fontSize: 16,
          color: RetroColors.vintageDarkBrown,
        ),
        bodyMedium: TextStyle(
          fontSize: 14,
          color: RetroColors.vintageDarkBrown,
        ),
      ),

      colorScheme: const ColorScheme(
        primary: RetroColors.vintageBrown,
        secondary: RetroColors.vintageRust,
        surface: RetroColors.vintageWhite,
        error: RetroColors.vintageBurgundy,
        onPrimary: RetroColors.vintageCream,
        onSecondary: RetroColors.vintageWhite,
        onSurface: RetroColors.vintageDarkBrown,
        onError: RetroColors.vintageWhite,
        brightness: Brightness.light,
      ),
    );
  }
}
