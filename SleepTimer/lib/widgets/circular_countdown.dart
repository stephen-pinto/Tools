import 'dart:math' as math;

import 'package:flutter/material.dart';

class CircularCountdown extends StatelessWidget {
  const CircularCountdown({
    super.key,
    required this.remaining,
    required this.total,
  });

  final Duration remaining;
  final Duration total;

  @override
  Widget build(BuildContext context) {
    final progress = total.inMilliseconds <= 0
        ? 0.0
        : (remaining.inMilliseconds / total.inMilliseconds).clamp(0.0, 1.0);

    return TweenAnimationBuilder<double>(
      tween: Tween<double>(begin: progress, end: progress),
      duration: const Duration(milliseconds: 450),
      curve: Curves.easeOutCubic,
      builder: (context, animatedProgress, _) {
        return AspectRatio(
          aspectRatio: 1,
          child: Stack(
            alignment: Alignment.center,
            children: [
              CustomPaint(
                size: const Size.square(280),
                painter: _CountdownRingPainter(progress: animatedProgress),
              ),
              Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    _formatDuration(remaining),
                    style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                      fontSize: 38,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 2,
                      shadows: const [
                        Shadow(color: Color(0x8048C6EF), blurRadius: 18),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    '${(animatedProgress * 100).round()}% remaining',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: Colors.white70,
                      letterSpacing: 0.5,
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );
  }

  static String _formatDuration(Duration duration) {
    final hours = duration.inHours.toString().padLeft(2, '0');
    final minutes = duration.inMinutes.remainder(60).toString().padLeft(2, '0');
    final seconds = duration.inSeconds.remainder(60).toString().padLeft(2, '0');
    return '$hours:$minutes:$seconds';
  }
}

class _CountdownRingPainter extends CustomPainter {
  _CountdownRingPainter({required this.progress});

  final double progress;

  @override
  void paint(Canvas canvas, Size size) {
    final center = size.center(Offset.zero);
    final strokeWidth = 18.0;
    final radius = (size.shortestSide - strokeWidth) / 2;
    final rect = Rect.fromCircle(center: center, radius: radius);

    final trackPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = strokeWidth
      ..strokeCap = StrokeCap.round
      ..color = Colors.white.withValues(alpha: 0.10);

    final progressPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = strokeWidth
      ..strokeCap = StrokeCap.round
      ..shader = SweepGradient(
        startAngle: -math.pi / 2,
        endAngle: math.pi * 3 / 2,
        colors: [
          const Color(0xFF68F6FF),
          const Color(0xFF48C6EF),
          const Color(0xFF4F7CFF),
          const Color(0xFF835CFF),
          const Color(0xFF68F6FF),
        ],
        stops: const [0.0, 0.35, 0.6, 0.85, 1.0],
      ).createShader(rect);

    canvas.drawCircle(center, radius, trackPaint);
    canvas.drawArc(
      rect,
      -math.pi / 2,
      math.pi * 2 * progress,
      false,
      progressPaint,
    );
  }

  @override
  bool shouldRepaint(covariant _CountdownRingPainter oldDelegate) {
    return oldDelegate.progress != progress;
  }
}
