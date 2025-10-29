import 'package:flutter/material.dart';

class RetroColors {
  // Primary vintage browns and earth tones
  static const Color vintageBrown = Color(0xFF7d5a3b);
  static const Color vintageDarkBrown = Color(0xFF4a3526);
  static const Color vintageLightBrown = Color(0xFFa67c52);
  static const Color vintageCream = Color(0xFFf4ead5);
  static const Color vintageBeige = Color(0xFFe8dcc4);
  static const Color vintageTan = Color(0xFFd4c4a8);
  
  // Accent colors
  static const Color vintageGold = Color(0xFFb8860b);
  static const Color vintageRust = Color(0xFFa85832);
  static const Color vintageOlive = Color(0xFF6b8e23);
  static const Color vintageBurgundy = Color(0xFF7c2d37);
  static const Color vintageGreen = Color(0xFF4a7c59);
  
  // Neutral tones
  static const Color vintageWhite = Color(0xFFfaf8f3);
  static const Color vintageOffWhite = Color(0xFFf5f1e8);
  static const Color vintageGray = Color(0xFF8b7d6b);
  static const Color vintageDarkGray = Color(0xFF5a4a3a);
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
      
      // Card Theme
      cardTheme: const CardThemeData(
        color: RetroColors.vintageWhite,
        elevation: 4,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.zero,
          side: BorderSide(
            color: RetroColors.vintageBrown,
            width: 3,
          ),
        ),
      ),
      
      // Elevated Button Theme
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: RetroColors.vintageBrown,
          foregroundColor: RetroColors.vintageCream,
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.zero,
            side: const BorderSide(
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
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: RetroColors.vintageWhite,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.zero,
          borderSide: const BorderSide(
            color: RetroColors.vintageBrown,
            width: 2,
          ),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.zero,
          borderSide: const BorderSide(
            color: RetroColors.vintageBrown,
            width: 2,
          ),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.zero,
          borderSide: const BorderSide(
            color: RetroColors.vintageRust,
            width: 2,
          ),
        ),
        labelStyle: const TextStyle(
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
