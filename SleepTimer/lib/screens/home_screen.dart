import 'dart:async';

import 'package:flutter/material.dart';
import 'package:sleep_timer/services/sleep_timer_service.dart';
import 'package:sleep_timer/widgets/circular_countdown.dart';
import 'package:sleep_timer/widgets/status_card.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  static const _backgroundColor = Color(0xFF0A0E21);
  static const _cardGradient = LinearGradient(
    colors: [Color(0xAA182547), Color(0x6624305B)],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  final SleepTimerService _service = SleepTimerService();
  final List<int> _hourValues = List<int>.generate(13, (index) => index);
  final List<int> _minuteValues = List<int>.generate(12, (index) => index * 5);

  late final FixedExtentScrollController _hourController;
  late final FixedExtentScrollController _minuteController;
  late final StreamSubscription<TimerState> _stateSubscription;
  late final StreamSubscription<Duration> _remainingSubscription;
  late final StreamSubscription<String> _logSubscription;

  Timer? _statusTimer;
  TimerState _timerState = TimerState.idle;
  Duration _remaining = Duration.zero;
  Duration _activeTotal = Duration.zero;
  int _selectedHours = 0;
  int _selectedMinutes = 30;
  String _latestLog = 'Set your timer and drift off peacefully.';
  bool? _musicPlaying;
  bool? _wifiEnabled;
  bool? _bluetoothEnabled;

  Duration get _selectedDuration =>
      Duration(hours: _selectedHours, minutes: _selectedMinutes);

  bool get _isRunning => _timerState == TimerState.running;
  bool get _isWorking => _timerState == TimerState.executing;
  bool get _showCountdown => _isRunning || _isWorking;
  bool get _canStart => _selectedDuration > Duration.zero && !_isWorking;

  @override
  void initState() {
    super.initState();
    _hourController = FixedExtentScrollController(initialItem: _selectedHours);
    _minuteController = FixedExtentScrollController(
      initialItem: _minuteValues.indexOf(_selectedMinutes),
    );
    _timerState = _service.state;
    _remaining = _service.remaining;
    _activeTotal = _service.total;

    _stateSubscription = _service.stateStream.listen(_handleStateChange);
    _remainingSubscription = _service.remainingStream.listen((remaining) {
      if (!mounted) return;
      setState(() {
        _remaining = remaining;
      });
    });
    _logSubscription = _service.logStream.listen((message) {
      if (!mounted) return;
      setState(() {
        _latestLog = message;
      });
    });

    _refreshSystemStatus();
    _statusTimer = Timer.periodic(
      const Duration(seconds: 8),
      (_) => _refreshSystemStatus(),
    );
  }

  void _handleStateChange(TimerState state) {
    if (!mounted) return;
    setState(() {
      _timerState = state;
      if (state == TimerState.running && _activeTotal == Duration.zero) {
        _activeTotal = _selectedDuration;
      }
    });

    if (state == TimerState.completed) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(const SnackBar(content: Text('Good night! 😴')));
      });
      _refreshSystemStatus();
    }
  }

  Future<void> _refreshSystemStatus() async {
    try {
      final status = await _service.getSystemStatus();
      if (!mounted) return;
      setState(() {
        _musicPlaying = status['music'];
        _bluetoothEnabled = status['bluetooth'];
        _wifiEnabled = status['wifi'];
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _musicPlaying ??= false;
        _bluetoothEnabled ??= false;
        _wifiEnabled ??= false;
      });
    }
  }

  void _toggleTimer() {
    if (_isRunning) {
      _service.cancel();
      return;
    }

    if (!_canStart) return;

    final duration = _selectedDuration;
    setState(() {
      _activeTotal = duration;
      _remaining = duration;
      _latestLog = '⏱️ Timer started: ${_formatShortDuration(duration)}';
    });
    _service.start(duration);
  }

  @override
  void dispose() {
    _statusTimer?.cancel();
    _stateSubscription.cancel();
    _remainingSubscription.cancel();
    _logSubscription.cancel();
    _hourController.dispose();
    _minuteController.dispose();
    _service.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      backgroundColor: _backgroundColor,
      appBar: AppBar(
        title: Text(
          '🌙 Sleep Timer',
          style: theme.textTheme.titleLarge?.copyWith(
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            colors: [Color(0xFF0A0E21), Color(0xFF121A3B), Color(0xFF0B1229)],
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
          ),
        ),
        child: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _buildHeroCard(theme),
                const SizedBox(height: 22),
                AnimatedSwitcher(
                  duration: const Duration(milliseconds: 500),
                  switchInCurve: Curves.easeOutCubic,
                  switchOutCurve: Curves.easeInCubic,
                  child: _showCountdown
                      ? _buildCountdownView(theme)
                      : _buildPickerView(theme),
                ),
                const SizedBox(height: 28),
                _buildActionButton(),
                const SizedBox(height: 20),
                _buildLogCard(theme),
                const SizedBox(height: 24),
                Text(
                  'System status',
                  style: theme.textTheme.titleMedium?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 14),
                Wrap(
                  spacing: 12,
                  runSpacing: 12,
                  children: [
                    SizedBox(
                      width: double.infinity,
                      child: StatusCard(
                        emoji: '🎵',
                        label: 'Music',
                        isActive: _musicPlaying ?? false,
                        statusText: _musicPlaying == null
                            ? 'Unknown'
                            : (_musicPlaying! ? 'Playing' : 'Paused'),
                      ),
                    ),
                    SizedBox(
                      width: double.infinity,
                      child: StatusCard(
                        emoji: '📶',
                        label: 'WiFi',
                        isActive: _wifiEnabled ?? false,
                        statusText: _wifiEnabled == null
                            ? 'Unknown'
                            : (_wifiEnabled! ? 'On' : 'Off'),
                      ),
                    ),
                    SizedBox(
                      width: double.infinity,
                      child: StatusCard(
                        emoji: '🔵',
                        label: 'Bluetooth',
                        isActive: _bluetoothEnabled ?? false,
                        statusText: _bluetoothEnabled == null
                            ? 'Unknown'
                            : (_bluetoothEnabled! ? 'On' : 'Off'),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildHeroCard(ThemeData theme) {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: _glassDecoration(),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Wind down beautifully',
            style: theme.textTheme.titleLarge?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            'Set a gentle countdown to pause music, turn off Bluetooth, and disconnect WiFi for the night.',
            style: theme.textTheme.bodyLarge?.copyWith(
              color: Colors.white70,
              height: 1.5,
            ),
          ),
          const SizedBox(height: 22),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(22),
              gradient: const LinearGradient(
                colors: [Color(0xFF1C2D55), Color(0xAA223E73)],
              ),
              border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
            ),
            child: Row(
              children: [
                const Icon(Icons.nights_stay_rounded, color: Color(0xFF68F6FF)),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    _showCountdown ? _statusLabel : _formatPickerSelection(),
                    style: theme.textTheme.titleMedium?.copyWith(
                      color: Colors.white,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPickerView(ThemeData theme) {
    return Container(
      key: const ValueKey('picker-view'),
      padding: const EdgeInsets.all(22),
      decoration: _glassDecoration(),
      child: Column(
        children: [
          Text(
            _formatPickerSelection(),
            style: theme.textTheme.headlineLarge?.copyWith(
              fontSize: 40,
              shadows: const [Shadow(color: Color(0x8048C6EF), blurRadius: 24)],
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Choose your sleep timer',
            style: theme.textTheme.bodyMedium?.copyWith(color: Colors.white70),
          ),
          const SizedBox(height: 24),
          Row(
            children: [
              Expanded(
                child: _buildWheelPicker(
                  label: 'Hours',
                  controller: _hourController,
                  itemCount: _hourValues.length,
                  onSelectedItemChanged: (index) {
                    setState(() {
                      _selectedHours = _hourValues[index];
                    });
                  },
                  itemBuilder: (context, index) => _buildWheelValue(
                    context,
                    value: _hourValues[index].toString().padLeft(2, '0'),
                    unit: 'hr',
                  ),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: _buildWheelPicker(
                  label: 'Minutes',
                  controller: _minuteController,
                  itemCount: _minuteValues.length,
                  onSelectedItemChanged: (index) {
                    setState(() {
                      _selectedMinutes = _minuteValues[index];
                    });
                  },
                  itemBuilder: (context, index) => _buildWheelValue(
                    context,
                    value: _minuteValues[index].toString().padLeft(2, '0'),
                    unit: 'min',
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildCountdownView(ThemeData theme) {
    final total = _activeTotal > Duration.zero
        ? _activeTotal
        : _selectedDuration;

    return Container(
      key: const ValueKey('countdown-view'),
      padding: const EdgeInsets.all(24),
      decoration: _glassDecoration(),
      child: Column(
        children: [
          CircularCountdown(remaining: _remaining, total: total),
          const SizedBox(height: 18),
          Text(
            _statusLabel,
            style: theme.textTheme.titleMedium?.copyWith(
              color: Colors.white70,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildWheelPicker({
    required String label,
    required FixedExtentScrollController controller,
    required int itemCount,
    required ValueChanged<int> onSelectedItemChanged,
    required IndexedWidgetBuilder itemBuilder,
  }) {
    return Container(
      height: 230,
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 16),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(28),
        gradient: const LinearGradient(
          colors: [Color(0xFF101935), Color(0x99203257)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
      ),
      child: Column(
        children: [
          Text(
            label,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
              color: Colors.white70,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 12),
          Expanded(
            child: ShaderMask(
              shaderCallback: (bounds) => const LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [
                  Colors.transparent,
                  Colors.white,
                  Colors.white,
                  Colors.transparent,
                ],
                stops: [0.0, 0.18, 0.82, 1.0],
              ).createShader(bounds),
              blendMode: BlendMode.dstIn,
              child: ListWheelScrollView.useDelegate(
                controller: controller,
                itemExtent: 56,
                diameterRatio: 1.25,
                perspective: 0.003,
                physics: const FixedExtentScrollPhysics(),
                overAndUnderCenterOpacity: 0.45,
                onSelectedItemChanged: onSelectedItemChanged,
                childDelegate: ListWheelChildBuilderDelegate(
                  builder: itemBuilder,
                  childCount: itemCount,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildWheelValue(
    BuildContext context, {
    required String value,
    required String unit,
  }) {
    return Center(
      child: RichText(
        text: TextSpan(
          style: Theme.of(context).textTheme.headlineMedium?.copyWith(
            fontSize: 34,
            fontWeight: FontWeight.w800,
            color: Colors.white,
            shadows: const [Shadow(color: Color(0x7048C6EF), blurRadius: 18)],
          ),
          children: [
            TextSpan(text: value),
            TextSpan(
              text: ' $unit',
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: Colors.white54,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildActionButton() {
    final isDisabled = !_isRunning && !_canStart;
    final gradient = _isRunning
        ? const LinearGradient(
            colors: [Color(0xFFFF6A88), Color(0xFFFF3F5E)],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          )
        : _isWorking
        ? const LinearGradient(colors: [Color(0xFF4A5678), Color(0xFF2B3554)])
        : const LinearGradient(
            colors: [Color(0xFF49E7A8), Color(0xFF38C8FF)],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          );

    final label = _isRunning
        ? 'Cancel'
        : _isWorking
        ? 'Executing...'
        : 'Start';

    return AnimatedOpacity(
      duration: const Duration(milliseconds: 250),
      opacity: isDisabled ? 0.45 : 1,
      child: IgnorePointer(
        ignoring: isDisabled || _isWorking,
        child: Center(
          child: GestureDetector(
            onTap: _toggleTimer,
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 350),
              curve: Curves.easeOutCubic,
              width: 170,
              height: 170,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: gradient,
                boxShadow: [
                  BoxShadow(
                    color:
                        (_isRunning
                                ? const Color(0x55FF4F7A)
                                : const Color(0x5549E7A8))
                            .withValues(alpha: _isWorking ? 0.18 : 0.38),
                    blurRadius: 36,
                    spreadRadius: 4,
                  ),
                ],
                border: Border.all(color: Colors.white.withValues(alpha: 0.18)),
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    _isRunning ? Icons.close_rounded : Icons.play_arrow_rounded,
                    color: Colors.white,
                    size: 48,
                  ),
                  const SizedBox(height: 10),
                  Text(
                    label,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 24,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildLogCard(ThemeData theme) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: _glassDecoration(),
      child: Row(
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: const BoxDecoration(
              shape: BoxShape.circle,
              gradient: LinearGradient(
                colors: [Color(0xFF68F6FF), Color(0xFF4F7CFF)],
              ),
            ),
            child: const Icon(Icons.auto_awesome_rounded, color: Colors.white),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Latest activity',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: Colors.white70,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  _latestLog,
                  style: theme.textTheme.bodyLarge?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  BoxDecoration _glassDecoration() {
    return BoxDecoration(
      borderRadius: BorderRadius.circular(30),
      gradient: _cardGradient,
      border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
      boxShadow: [
        BoxShadow(
          color: Colors.black.withValues(alpha: 0.22),
          blurRadius: 28,
          offset: const Offset(0, 16),
        ),
      ],
    );
  }

  String get _statusLabel {
    switch (_timerState) {
      case TimerState.running:
        return 'Counting down to sleep mode';
      case TimerState.executing:
        return 'Turning everything off for the night';
      case TimerState.completed:
        return 'Sleep routine complete';
      case TimerState.idle:
        return 'Ready when you are';
    }
  }

  String _formatPickerSelection() {
    final hours = _selectedHours.toString().padLeft(2, '0');
    final minutes = _selectedMinutes.toString().padLeft(2, '0');
    return '$hours h  $minutes m';
  }

  String _formatShortDuration(Duration duration) {
    final hours = duration.inHours;
    final minutes = duration.inMinutes.remainder(60);
    if (hours == 0) {
      return '${minutes}m';
    }
    return '${hours}h ${minutes}m';
  }
}
