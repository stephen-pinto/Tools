import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:sleep_timer/main.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const channel = MethodChannel('com.tools.sleep_timer/system_control');

  setUp(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, (call) async {
          switch (call.method) {
            case 'isMusicPlaying':
              return true;
            case 'getBluetoothState':
              return true;
            case 'getWifiState':
              return true;
            default:
              return false;
          }
        });
  });

  tearDown(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, null);
  });

  testWidgets('Sleep timer home screen renders', (WidgetTester tester) async {
    await tester.pumpWidget(const MyApp());
    await tester.pumpAndSettle();

    expect(find.text('🌙 Sleep Timer'), findsOneWidget);
    expect(find.text('Start'), findsOneWidget);
    expect(find.text('System status'), findsOneWidget);
  });
}
