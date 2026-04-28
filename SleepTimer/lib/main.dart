import 'package:flutter/material.dart';
import 'package:sleep_timer/screens/home_screen.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    const background = Color(0xFF0A0E21);
    const surface = Color(0xFF121933);
    const accent = Color(0xFF68F6FF);

    final baseTheme = ThemeData(
      brightness: Brightness.dark,
      useMaterial3: true,
      scaffoldBackgroundColor: background,
      colorScheme: const ColorScheme.dark(
        primary: accent,
        secondary: Color(0xFF48C6EF),
        surface: surface,
      ),
      textTheme: const TextTheme(
        headlineLarge: TextStyle(
          fontSize: 42,
          fontWeight: FontWeight.w700,
          letterSpacing: -1,
          color: Colors.white,
        ),
        headlineMedium: TextStyle(
          fontSize: 30,
          fontWeight: FontWeight.w700,
          color: Colors.white,
        ),
        titleLarge: TextStyle(
          fontSize: 20,
          fontWeight: FontWeight.w600,
          color: Colors.white,
        ),
        bodyLarge: TextStyle(fontSize: 16, color: Color(0xFFD6E4FF)),
        bodyMedium: TextStyle(fontSize: 14, color: Color(0xFFA9B6D3)),
      ),
      snackBarTheme: const SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: Color(0xFF182344),
        contentTextStyle: TextStyle(color: Colors.white),
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: Colors.transparent,
        elevation: 0,
        centerTitle: false,
        foregroundColor: Colors.white,
      ),
    );

    return MaterialApp(
      title: 'Sleep Timer',
      debugShowCheckedModeBanner: false,
      theme: baseTheme.copyWith(
        cardTheme: CardThemeData(
          color: Colors.white.withValues(alpha: 0.06),
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(28),
            side: BorderSide(color: Colors.white.withValues(alpha: 0.10)),
          ),
        ),
      ),
      home: const HomeScreen(),
    );
  }
}
