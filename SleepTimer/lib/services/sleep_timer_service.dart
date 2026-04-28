import 'dart:async';
import 'system_control_service.dart';

enum TimerState { idle, running, executing, completed }

class SleepTimerService {
  final SystemControlService _systemControl = SystemControlService();

  Timer? _timer;
  Duration _remaining = Duration.zero;
  Duration _total = Duration.zero;
  TimerState _state = TimerState.idle;

  // Stream controllers for state updates
  final _stateController = StreamController<TimerState>.broadcast();
  final _remainingController = StreamController<Duration>.broadcast();
  final _logController = StreamController<String>.broadcast();

  Stream<TimerState> get stateStream => _stateController.stream;
  Stream<Duration> get remainingStream => _remainingController.stream;
  Stream<String> get logStream => _logController.stream;

  TimerState get state => _state;
  Duration get remaining => _remaining;
  Duration get total => _total;

  /// Starts the sleep timer with the given duration.
  void start(Duration duration) {
    if (_state == TimerState.running) return;

    _total = duration;
    _remaining = duration;
    _state = TimerState.running;
    _stateController.add(_state);
    _remainingController.add(_remaining);
    _logController.add('⏱️ Timer started: ${_formatDuration(duration)}');

    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      _remaining -= const Duration(seconds: 1);
      _remainingController.add(_remaining);

      if (_remaining <= Duration.zero) {
        _timer?.cancel();
        _executeActions();
      }
    });
  }

  /// Cancels the running timer.
  void cancel() {
    _timer?.cancel();
    _timer = null;
    _remaining = Duration.zero;
    _total = Duration.zero;
    _state = TimerState.idle;
    _stateController.add(_state);
    _remainingController.add(_remaining);
    _logController.add('❌ Timer cancelled');
  }

  /// Executes the sleep actions: pause music → disable BT → disable WiFi.
  Future<void> _executeActions() async {
    _state = TimerState.executing;
    _stateController.add(_state);
    _logController.add('🌙 Executing sleep actions...');

    // Step 1: Pause music if playing
    final isMusicOn = await _systemControl.isMusicPlaying();
    if (isMusicOn) {
      _logController.add('🎵 Pausing music...');
      final paused = await _systemControl.pauseMusic();
      _logController.add(paused ? '✅ Music paused' : '⚠️ Could not pause music');
      // Small delay to let audio system settle
      await Future.delayed(const Duration(milliseconds: 500));
    } else {
      _logController.add('🎵 No music playing');
    }

    // Step 2: Disable Bluetooth
    final isBtOn = await _systemControl.isBluetoothEnabled();
    if (isBtOn) {
      _logController.add('🔵 Disabling Bluetooth...');
      final btOff = await _systemControl.disableBluetooth();
      _logController.add(btOff ? '✅ Bluetooth disabled' : '⚠️ Could not disable Bluetooth');
    } else {
      _logController.add('🔵 Bluetooth already off');
    }

    // Step 3: Disable WiFi
    final isWifiOn = await _systemControl.isWifiEnabled();
    if (isWifiOn) {
      _logController.add('📶 Disabling WiFi...');
      final wifiOff = await _systemControl.disableWifi();
      _logController.add(wifiOff ? '✅ WiFi disabled' : '⚠️ Could not disable WiFi (check settings)');
    } else {
      _logController.add('📶 WiFi already off');
    }

    _logController.add('🌙 Sleep actions complete. Good night! 😴');
    _state = TimerState.completed;
    _stateController.add(_state);
  }

  String _formatDuration(Duration d) {
    final hours = d.inHours;
    final minutes = d.inMinutes.remainder(60);
    final seconds = d.inSeconds.remainder(60);
    if (hours > 0) {
      return '${hours}h ${minutes}m ${seconds}s';
    }
    return '${minutes}m ${seconds}s';
  }

  /// Call this to get current system status (for UI indicators).
  Future<Map<String, bool>> getSystemStatus() async {
    final results = await Future.wait([
      _systemControl.isMusicPlaying(),
      _systemControl.isBluetoothEnabled(),
      _systemControl.isWifiEnabled(),
    ]);
    return {
      'music': results[0],
      'bluetooth': results[1],
      'wifi': results[2],
    };
  }

  void dispose() {
    _timer?.cancel();
    _stateController.close();
    _remainingController.close();
    _logController.close();
  }
}
