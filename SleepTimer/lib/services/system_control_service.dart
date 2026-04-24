import 'dart:developer' as dev;
import 'package:flutter/services.dart';

class SystemControlService {
  static const _channel = MethodChannel('com.tools.sleep_timer/system_control');

  /// Pauses any currently playing music by requesting audio focus.
  /// Returns true if music was successfully paused.
  Future<bool> pauseMusic() async {
    try {
      final result = await _channel.invokeMethod<bool>('pauseMusic');
      return result ?? false;
    } on PlatformException catch (e) {
      dev.log('Failed to pause music: ${e.message}', name: 'SystemControl');
      return false;
    }
  }

  /// Disables Bluetooth.
  /// Returns true if BT was disabled, false otherwise.
  Future<bool> disableBluetooth() async {
    try {
      final result = await _channel.invokeMethod<dynamic>('disableBluetooth');
      return result == true;
    } on PlatformException catch (e) {
      dev.log('Failed to disable Bluetooth: ${e.message}', name: 'SystemControl');
      return false;
    }
  }

  /// Disables WiFi.
  /// Returns true if WiFi was disabled.
  /// Returns false if it couldn't be disabled (user may have been sent to settings).
  Future<bool> disableWifi() async {
    try {
      final result = await _channel.invokeMethod<dynamic>('disableWifi');
      if (result == "settings") {
        return false;
      }
      return result == true;
    } on PlatformException catch (e) {
      dev.log('Failed to disable WiFi: ${e.message}', name: 'SystemControl');
      return false;
    }
  }

  /// Returns true if Bluetooth is currently enabled.
  Future<bool> isBluetoothEnabled() async {
    try {
      final result = await _channel.invokeMethod<bool>('getBluetoothState');
      return result ?? false;
    } on PlatformException {
      return false;
    }
  }

  /// Returns true if WiFi is currently enabled.
  Future<bool> isWifiEnabled() async {
    try {
      final result = await _channel.invokeMethod<bool>('getWifiState');
      return result ?? false;
    } on PlatformException {
      return false;
    }
  }

  /// Returns true if music is currently playing.
  Future<bool> isMusicPlaying() async {
    try {
      final result = await _channel.invokeMethod<bool>('isMusicPlaying');
      return result ?? false;
    } on PlatformException {
      return false;
    }
  }
}
